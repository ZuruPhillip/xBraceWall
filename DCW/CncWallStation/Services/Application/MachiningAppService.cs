using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 加工控制应用服务实现
    /// </summary>
    public class MachiningAppService : IMachiningAppService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<MachiningAppService> _logger;

        public MachiningAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<MachiningAppService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<WallQueueItemDto>> GetWallQueueAsync(int topCount = 5)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var queue = await db.Walls
                .AsNoTracking()
                .Where(w => w.Status == (int)ProcessStatus.待加工)
                .OrderByDescending(w => w.Priority)
                .ThenBy(w => w.ImportTime)
                .Take(topCount)
                .Select(w => new WallQueueItemDto
                {
                    Id = w.Id,
                    WallId = w.WallId,
                    ProjectName = w.ProjectName,
                    Floor = w.Floor,
                    Priority = w.Priority,
                    Status = w.Status
                })
                .ToListAsync();

            _logger.LogInformation("加载加工队列: {Count} 条", queue.Count);
            return queue;
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
                StatusText = ((ProcessStatus)wall.Status).ToDisplayText(),
                UpdatedBy = wall.UpdatedBy
            };
        }

        /// <inheritdoc/>
        public async Task StartMachiningAsync(long wallId, string operatorName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.SetProductionTime(startTime: DateTime.Now, endTime: null, operatorName);
            wall.UpdateStatus((int)ProcessStatus.加工中, operatorName);
            await db.SaveChangesAsync();

            _logger.LogInformation("开始加工: WallId={WallId}, Operator={Operator}", wall.WallId, operatorName);
        }

        /// <inheritdoc/>
        public async Task PauseMachiningAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateStatus((int)ProcessStatus.暂停, "system");
            await db.SaveChangesAsync();

            _logger.LogInformation("暂停加工: WallId={WallId}", wall.WallId);
        }

        /// <inheritdoc/>
        public async Task ResumeMachiningAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateStatus((int)ProcessStatus.加工中, "system");
            await db.SaveChangesAsync();

            _logger.LogInformation("恢复加工: WallId={WallId}", wall.WallId);
        }

        /// <inheritdoc/>
        public async Task EmergencyStopAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateStatus((int)ProcessStatus.暂停, "system");
            await db.SaveChangesAsync();

            _logger.LogWarning("急停: WallId={WallId}", wall.WallId);
        }

        /// <inheritdoc/>
        public async Task ResetMachiningAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.SetProductionTime(startTime: null, endTime: null, "system");
            wall.UpdateStatus((int)ProcessStatus.待加工, "system");
            await db.SaveChangesAsync();

            _logger.LogInformation("复位加工: WallId={WallId}", wall.WallId);
        }

        /// <inheritdoc/>
        public async Task MarkExceptionAsync(long wallId, string operatorName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateStatus((int)ProcessStatus.中止, operatorName);
            await db.SaveChangesAsync();

            _logger.LogWarning("标记异常: WallId={WallId}, Operator={Operator}", wall.WallId, operatorName);
        }

        /// <inheritdoc/>
        public async Task CompleteMachiningAsync(long wallId, string operatorName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            var now = DateTime.Now;
            wall.SetProductionTime(null, now, operatorName);
            wall.UpdateStatus((int)ProcessStatus.待质检, operatorName);

            // 生成加工记录
            var totalSeconds = wall.EndProductionTime.HasValue && wall.StartProductionTime.HasValue
                ? (long)(wall.EndProductionTime.Value - wall.StartProductionTime.Value).TotalSeconds
                : (long?)null;

            db.MachiningRecords.Add(new MachiningRecordEntity(
                wallId,
                operatorName,
                wall.StartProductionTime ?? now,
                now,
                totalSeconds,
                (int)ProcessStatus.待质检));

            await db.SaveChangesAsync();

            _logger.LogInformation("完成加工: WallId={WallId}, Operator={Operator}, 进入待质检", wall.WallId, operatorName);
        }

        /// <inheritdoc/>
        public async Task RecoverFromExceptionAsync(long wallId, string operatorName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateStatus((int)ProcessStatus.待加工, operatorName);
            await db.SaveChangesAsync();

            _logger.LogInformation("从异常恢复: WallId={WallId}, Operator={Operator}", wall.WallId, operatorName);
        }

        /// <inheritdoc/>
        public async Task<MachiningRecordEntity?> GetMachiningRecordAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.MachiningRecords
                .AsNoTracking()
                .Where(r => r.WallId == wallId)
                .OrderByDescending(r => r.StartTime)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<int> GetInstructionCountAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.PlcInstructions
                .AsNoTracking()
                .Where(i => i.WallId == wallId)
                .CountAsync();
        }

        /// <inheritdoc/>
        public async Task<List<WallQueueItemDto>> SearchWallsByWallIdAsync(string keyword)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var results = await db.Walls
                .AsNoTracking()
                .Where(w => w.WallId.Contains(keyword))
                .OrderByDescending(w => w.Priority)
                .ThenBy(w => w.ImportTime)
                .Take(20)
                .Select(w => new WallQueueItemDto
                {
                    Id = w.Id,
                    WallId = w.WallId,
                    ProjectName = w.ProjectName,
                    Floor = w.Floor,
                    Priority = w.Priority,
                    Status = w.Status
                })
                .ToListAsync();

            _logger.LogInformation("墙体模糊搜索: keyword={Keyword}, results={Count}", keyword, results.Count);
            return results;
        }
    }
}
