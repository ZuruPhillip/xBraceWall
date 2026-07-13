using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.MomWallData;
using CncWallStation.Plcs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// PLC 数据应用服务实现
    /// </summary>
    public class PlcDataAppService : IPlcDataAppService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<PlcDataAppService> _logger;

        public PlcDataAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<PlcDataAppService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<WallInfoDto?> GetWallInfoAsync(string wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WallId == wallId);

            if (wall == null)
            {
                _logger.LogWarning("未查到墙体: WallId={WallId}", wallId);
                return null;
            }

            return new WallInfoDto
            {
                Id = wall.Id,
                WallId = wall.WallId,
                WallName = wall.WallName,
                SchemaVersion = wall.SchemaVersion,
                AuditStatus = wall.AuditStatus,
                ProjectName = wall.ProjectName,
                Floor = wall.Floor,
                BimJsonData = wall.BimJsonData,
                MomJsonData = wall.MomJsonData,
                PipelineStage = wall.PipelineStage.ToDisplayText(),
                PipelineStageText = wall.PipelineStage.ToDisplayText(),
                Priority = wall.Priority,
                ImportTime = wall.ImportTime,
                Status = wall.Status,
                StatusText = ((Models.ProcessStatus)wall.Status).ToDisplayText(),
                UpdatedBy = wall.UpdatedBy
            };
        }

        /// <inheritdoc/>
        public async Task<PlcGenerationResult> GeneratePlcInstructionsGroupedAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == wallId);

            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            if (string.IsNullOrWhiteSpace(wall.MomJsonData))
                throw new InvalidOperationException(
                    "墙体的 MOM 数据尚未生成，无法计算 PLC 指令。\n\n" +
                    "请先在 WallListPage 中对该墙体【执行管线】操作，完成 BimJSON → MomJSON 转换后再试。");

            // ★ 用 System.Text.Json，匹配 Feature 基类上的 [JsonPolymorphic] / [JsonDerivedType]
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,               // 格式化缩进
                Encoder = JavaScriptEncoder   // 保留中文，不转义
                                         .UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            MomWall? momWall;
            try
            {
                momWall = JsonSerializer.Deserialize<MomWall>(wall.MomJsonData, options);

                if (momWall != null)
                {
                    foreach (var f in momWall.Features)
                        f.RestoreFaceFromInitialSide();
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"MomJsonData 反序列化失败，可能存在数据损坏。请重新执行管线操作。\n\n详情：{ex.Message}", ex);
            }

            if (momWall == null)
                throw new InvalidOperationException("MomJsonData 反序列化失败，可能存在数据损坏。请重新执行管线操作。");

            // ★ 原点变换：使特征坐标和切削面为变换后的值
            momWall.ApplyOriginTransform();

            // ★ 按切削面分类特征为正面/反面
            float wallThickness = momWall.Thickness;
            var frontFeatures = momWall.Features
                .Where(f => FeatureSideClassifier.IsFront(f, wallThickness))
                .ToList();
            var backFeatures = momWall.Features
                .Where(f => !FeatureSideClassifier.IsFront(f, wallThickness))
                .ToList();

            _logger.LogInformation(
                "PLC 特征分类: WallId={WallId}, 正面特征={FrontCount}, 反面特征={BackCount}",
                wallId, frontFeatures.Count, backFeatures.Count);

            // ★ 正反面分别生成 PLC 指令（正面 D=1，反面 D=5）
            var result = new PlcGenerationResult
            {
                FrontGroups = WallPlcConverter.ConvertGrouped(momWall, frontFeatures, 1),
                BackGroups = WallPlcConverter.ConvertGrouped(momWall, backFeatures, 5)
            };

            return result;
        }

        /// <inheritdoc/>
        public async Task<List<PlcInstructionEntity>> LoadInstructionsAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.Set<PlcInstructionEntity>()
                .AsNoTracking()
                .Where(i => i.WallId == wallId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task SaveDraftAsync(long wallId, List<PlcInstructionEntity> instructions, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var now = DateTime.Now;

            // 1. 删除该墙体所有旧指令
            await db.Set<PlcInstructionEntity>()
                .Where(i => i.WallId == wallId)
                .ExecuteDeleteAsync();

            // 2. 批量插入新指令（Id 由数据库自增生成）
            int sortOrder = 0;
            foreach (var item in instructions)
            {
                db.Set<PlcInstructionEntity>().Add(new PlcInstructionEntity
                {
                    WallId = wallId,
                    T = item.T,
                    F = item.F,
                    D = item.D,
                    X0 = item.X0,
                    Y0 = item.Y0,
                    Z0 = item.Z0,
                    X1 = item.X1,
                    Y1 = item.Y1,
                    Z1 = item.Z1,
                    SortOrder = sortOrder++,
                    Side = item.Side,
                    HandlerName = item.HandlerName,
                    FeatureName = item.FeatureName,
                    UpdatedBy = updatedBy,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("保存PLC指令草稿: WallId={WallId}, 指令数={Count}", wallId, instructions.Count);
        }
    }
}
