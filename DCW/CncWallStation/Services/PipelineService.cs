using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.VersionMappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CncWallStation.Services
{
    /// <summary>
    /// 管线服务接口
    /// </summary>
    public interface IPipelineService
    {
        /// <summary>手动执行完整管线：ValidateBim → ConvertToMom → ValidateMom</summary>
        Task<PipelineResult> ExecutePipelineAsync(long wallId);

        /// <summary>对导入批次中指定状态的墙体批量执行管线</summary>
        Task<int> BatchExecuteAsync(int projectId);
    }

    /// <summary>管线执行结果</summary>
    public class PipelineResult
    {
        public PipelineStage FinalStage { get; set; }
        public string GroupId { get; set; } = string.Empty;
        public List<ValidationErrorEntry> Errors { get; set; } = new();
        public string? MomJsonData { get; set; }
    }

    /// <summary>校验错误条目（DTO）</summary>
    public class ValidationErrorEntry
    {
        public PipelineStage Stage { get; set; }
        public string? ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 管线服务实现（基于 IDbContextFactory，每次管线执行使用独立 DbContext）
    /// </summary>
    public class PipelineService : IPipelineService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly BimWallMapperFactory _mapperFactory;
        private readonly ILogger<PipelineService> _logger;

        public PipelineService(
            IDbContextFactory<AppDbContext> dbFactory,
            BimWallMapperFactory mapperFactory,
            ILogger<PipelineService> logger)
        {
            _dbFactory = dbFactory;
            _mapperFactory = mapperFactory;
            _logger = logger;
        }

        // ==================== 主入口：执行单个墙体管线 ====================

        public async Task<PipelineResult> ExecutePipelineAsync(long wallId)
        {
            // 每次管线执行使用独立 DbContext（足够长寿命覆盖整个管线，但不跨方法共享）
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await ExecutePipelineInternalAsync(db, wallId);
        }

        // ==================== 批量执行 ====================

        public async Task<int> BatchExecuteAsync(int projectId)
        {
            // 先用一个独立 DbContext 查询待处理列表（短生命周期）
            List<long> wallIds;
            await using (var db = await _dbFactory.CreateDbContextAsync())
            {
                wallIds = await db.Walls
                    .AsNoTracking()
                    .Where(w => w.ProjectId == projectId &&
                                (w.PipelineStage == PipelineStage.Imported ||
                                 w.PipelineStage == PipelineStage.BimInvalid ||
                                 w.PipelineStage == PipelineStage.ConversionFailed ||
                                 w.PipelineStage == PipelineStage.MomInvalid))
                    .Select(w => w.Id)
                    .ToListAsync();
            }

            int successCount = 0;
            foreach (var id in wallIds)
            {
                try
                {
                    // 每个墙体使用独立 DbContext，互不影响
                    await using var db = await _dbFactory.CreateDbContextAsync();
                    var result = await ExecutePipelineInternalAsync(db, id);
                    if (result.FinalStage == PipelineStage.Ready)
                        successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "批量管线执行异常: WallId={WallId}", id);
                }
            }

            _logger.LogInformation("批量管线完成: 成功 {Success}/{Total}", successCount, wallIds.Count);
            return successCount;
        }

        // ==================== 核心管线逻辑（共享 DbContext） ====================

        private async Task<PipelineResult> ExecutePipelineInternalAsync(AppDbContext db, long wallId)
        {
            var groupId = Guid.NewGuid().ToString("N");
            var result = new PipelineResult { GroupId = groupId };

            // 加载实体（带跟踪，便于后续修改）
            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
            {
                result.FinalStage = PipelineStage.Imported;
                result.Errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.Imported,
                    ErrorMessage = $"墙体 ID={wallId} 不存在"
                });
                return result;
            }

            _logger.LogInformation("开始执行管线: WallId={WallId}, GroupId={GroupId}", wallId, groupId);
            var updatedBy = wall.UpdatedBy ?? Environment.UserName;

            // ========== 步骤 1：校验 BimJSON ==========
            wall.UpdatePipelineStage(PipelineStage.ValidatingBim);
            await db.SaveChangesAsync();

            var bimErrors = ValidateBimJson(wall.BimJsonData);
            if (bimErrors.Count > 0)
            {
                wall.UpdatePipelineStage(PipelineStage.BimInvalid);

                var errorEntities = bimErrors.Select(e => new ValidationErrorEntity(
                    wallId, groupId, PipelineStage.ValidatingBim, e.ErrorMessage, e.ErrorCode))
                    .ToList();
                await db.ValidationErrors.AddRangeAsync(errorEntities);
                await db.SaveChangesAsync();

                result.FinalStage = PipelineStage.BimInvalid;
                result.Errors = bimErrors;
                _logger.LogWarning("BimJSON 校验失败: WallId={WallId}, 错误数={Count}", wallId, bimErrors.Count);
                return result;
            }

            wall.UpdatePipelineStage(PipelineStage.BimValid);
            await db.SaveChangesAsync();
            _logger.LogInformation("BimJSON 校验通过: WallId={WallId}", wallId);

            // ========== 步骤 2：转换 BimJSON → MomJSON ==========
            wall.UpdatePipelineStage(PipelineStage.Converting);
            await db.SaveChangesAsync();

            var convertResult = ConvertToMom(wall.BimJsonData);
            if (!convertResult.Success)
            {
                wall.UpdatePipelineStage(PipelineStage.ConversionFailed);

                await db.ValidationErrors.AddAsync(new ValidationErrorEntity(
                    wallId, groupId, PipelineStage.Converting,
                    convertResult.ErrorMessage ?? "转换失败", "CONVERT_FAILED"));
                await db.SaveChangesAsync();

                result.FinalStage = PipelineStage.ConversionFailed;
                result.Errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.Converting,
                    ErrorCode = "CONVERT_FAILED",
                    ErrorMessage = convertResult.ErrorMessage ?? "转换失败"
                });
                _logger.LogWarning("BimJSON→MomJSON 转换失败: WallId={WallId}", wallId);
                return result;
            }

            // 通过领域方法更新 MomJsonData + 阶段
            wall.UpdateMomJsonData(convertResult.MomJsonData!);
            wall.UpdatePipelineStage(PipelineStage.Converted);
            await db.SaveChangesAsync();
            _logger.LogInformation("BimJSON→MomJSON 转换完成: WallId={WallId}", wallId);

            // ========== 步骤 3：校验 MomJSON ==========
            wall.UpdatePipelineStage(PipelineStage.ValidatingMom);
            await db.SaveChangesAsync();

            var momErrors = ValidateMomJson(convertResult.MomJsonData!);
            if (momErrors.Count > 0)
            {
                wall.UpdatePipelineStage(PipelineStage.MomInvalid);

                var errorEntities = momErrors.Select(e => new ValidationErrorEntity(
                    wallId, groupId, PipelineStage.ValidatingMom, e.ErrorMessage, e.ErrorCode))
                    .ToList();
                await db.ValidationErrors.AddRangeAsync(errorEntities);
                await db.SaveChangesAsync();

                result.FinalStage = PipelineStage.MomInvalid;
                result.Errors = momErrors;
                result.MomJsonData = convertResult.MomJsonData;
                _logger.LogWarning("MomJSON 校验失败: WallId={WallId}, 错误数={Count}", wallId, momErrors.Count);
                return result;
            }

            wall.UpdatePipelineStage(PipelineStage.MomValid);

            // ========== 全部通过 → Ready ==========
            wall.UpdatePipelineStage(PipelineStage.Ready);
            wall.UpdateStatus((int)ProcessStatus.待加工, updatedBy);
            await db.SaveChangesAsync();

            result.FinalStage = PipelineStage.Ready;
            result.MomJsonData = convertResult.MomJsonData;
            _logger.LogInformation("管线全部通过 → Ready: WallId={WallId}", wallId);

            return result;
        }

        // ==================== BimJSON 校验（纯函数，不变） ====================

        private List<ValidationErrorEntry> ValidateBimJson(string bimJsonData)
        {
            var errors = new List<ValidationErrorEntry>();

            if (string.IsNullOrWhiteSpace(bimJsonData))
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingBim,
                    ErrorCode = "BIM_EMPTY",
                    ErrorMessage = "BimJSON 数据为空"
                });
                return errors;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(bimJsonData);
                var root = doc.RootElement;

                if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingBim,
                        ErrorCode = "BIM_NOT_OBJECT",
                        ErrorMessage = "BimJSON 根节点必须是 JSON 对象"
                    });
                    return errors;
                }

                bool hasId = root.TryGetProperty("id", out _) ||
                             root.TryGetProperty("wallId", out _) ||
                             root.TryGetProperty("wall_id", out _);

                if (!hasId)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingBim,
                        ErrorCode = "BIM_NO_ID",
                        ErrorMessage = "BimJSON 缺少墙体标识字段 (id/wallId/wall_id)"
                    });
                }

                if (root.TryGetProperty("features", out var features))
                {
                    if (features.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        errors.Add(new ValidationErrorEntry
                        {
                            Stage = PipelineStage.ValidatingBim,
                            ErrorCode = "BIM_FEATURES_NOT_ARRAY",
                            ErrorMessage = "BimJSON 中 'features' 字段必须是数组"
                        });
                    }
                    else
                    {
                        int idx = 0;
                        foreach (var feature in features.EnumerateArray())
                        {
                            if (feature.ValueKind != System.Text.Json.JsonValueKind.Object)
                            {
                                errors.Add(new ValidationErrorEntry
                                {
                                    Stage = PipelineStage.ValidatingBim,
                                    ErrorCode = "BIM_FEATURE_NOT_OBJECT",
                                    ErrorMessage = $"features[{idx}] 不是有效对象"
                                });
                            }
                            else if (!feature.TryGetProperty("type", out _))
                            {
                                errors.Add(new ValidationErrorEntry
                                {
                                    Stage = PipelineStage.ValidatingBim,
                                    ErrorCode = "BIM_FEATURE_NO_TYPE",
                                    ErrorMessage = $"features[{idx}] 缺少 'type' 字段"
                                });
                            }
                            idx++;
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingBim,
                    ErrorCode = "BIM_PARSE_ERROR",
                    ErrorMessage = $"BimJSON 解析失败: {ex.Message}"
                });
            }

            return errors;
        }

        // ==================== BimJSON → MomJSON 转换（版本控制） ====================

        /// <summary>
        /// BimJSON → MomJSON 转换（版本控制）
        /// 参考 ControllerPageViewModel.ConvertToMom 实现逻辑：
        /// 1. BimDataVersionResolver.ResolveVersion 解析版本
        /// 2. BimWallMapperFactory.GetMapper 获取对应版本 Mapper
        /// 3. mapper.Map 执行转换
        /// </summary>
        private (bool Success, string? MomJsonData, string? ErrorMessage) ConvertToMom(string bimJsonData)
        {
            try
            {
                // 1. 解析版本
                string version = BimDataVersionResolver.ResolveVersion(bimJsonData);
                _logger.LogInformation("检测到 BimData 版本：v{Version}", version);

                // 2. 获取对应版本的 Mapper
                IBimWallMapper mapper = _mapperFactory.GetMapper(version);

                // 3. 转换 BimJson → MomWall 领域对象
                var momWall = mapper.Map(bimJsonData);

                // ── 3. 序列化 ─────────────────────────────────────────
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,               // 格式化缩进
                    Encoder = JavaScriptEncoder   // 保留中文，不转义
                                         .UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };


                string momWallJson = JsonSerializer.Serialize(momWall, options);

                var momJson = JsonSerializer.Serialize(momWall, options);

                return (true, momJson, null);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "不支持的 BimData 版本");
                return (false, null, $"不支持的 BimData 版本：{ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BimJSON → MomJSON 版本控制转换异常");
                return (false, null, $"转换异常: {ex.Message}");
            }
        }

        // ==================== MomJSON 校验（纯函数，不变） ====================

        private List<ValidationErrorEntry> ValidateMomJson(string momJsonData)
        {
            var errors = new List<ValidationErrorEntry>();

            if (string.IsNullOrWhiteSpace(momJsonData))
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingMom,
                    ErrorCode = "MOM_EMPTY",
                    ErrorMessage = "MomJSON 数据为空"
                });
                return errors;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(momJsonData);
                var root = doc.RootElement;

                if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NOT_OBJECT",
                        ErrorMessage = "MomJSON 根节点必须是 JSON 对象"
                    });
                    return errors;
                }

                if (!root.TryGetProperty("id", out var idProp) || string.IsNullOrWhiteSpace(idProp.GetString()))
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NO_ID",
                        ErrorMessage = "MomJSON 缺少有效 'id' 字段"
                    });
                }

                //if (root.TryGetProperty("momFeatures", out var momFeatures))
                //{
                //    if (momFeatures.ValueKind != System.Text.Json.JsonValueKind.Array)
                //    {
                //        errors.Add(new ValidationErrorEntry
                //        {
                //            Stage = PipelineStage.ValidatingMom,
                //            ErrorCode = "MOM_FEATURES_NOT_ARRAY",
                //            ErrorMessage = "MomJSON 中 'momFeatures' 字段必须是数组"
                //        });
                //    }
                //    else
                //    {
                //        int idx = 0;
                //        foreach (var feature in momFeatures.EnumerateArray())
                //        {
                //            if (feature.ValueKind != System.Text.Json.JsonValueKind.Object)
                //            {
                //                errors.Add(new ValidationErrorEntry
                //                {
                //                    Stage = PipelineStage.ValidatingMom,
                //                    ErrorCode = "MOM_FEATURE_NOT_OBJECT",
                //                    ErrorMessage = $"momFeatures[{idx}] 不是有效对象"
                //                });
                //            }
                //            idx++;
                //        }
                //    }
                //}
                //else
                //{
                //    errors.Add(new ValidationErrorEntry
                //    {
                //        Stage = PipelineStage.ValidatingMom,
                //        ErrorCode = "MOM_NO_FEATURES",
                //        ErrorMessage = "MomJSON 缺少 'momFeatures' 字段"
                //    });
                //}

                //if (!root.TryGetProperty("convertedAt", out _))
                //{
                //    errors.Add(new ValidationErrorEntry
                //    {
                //        Stage = PipelineStage.ValidatingMom,
                //        ErrorCode = "MOM_NO_CONVERTEDAT",
                //        ErrorMessage = "MomJSON 缺少 'convertedAt' 元数据"
                //    });
                //}
            }
            catch (System.Text.Json.JsonException ex)
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingMom,
                    ErrorCode = "MOM_PARSE_ERROR",
                    ErrorMessage = $"MomJSON 解析失败: {ex.Message}"
                });
            }

            return errors;
        }
    }
}