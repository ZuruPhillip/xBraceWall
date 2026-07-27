using AutoMapper;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 墙体查询应用服务（基于 IDbContextFactory，每个方法独立 DbContext，彻底解决并发问题）
    /// </summary>
    public class WallAppService : IWallAppService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<WallAppService> _logger;

        public WallAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            IMapper mapper,
            ILogger<WallAppService> logger)
        {
            _dbFactory = dbFactory;
            _mapper = mapper;
            _logger = logger;
        }

        // ==================== 查询 ====================

        public async Task<PagedResultDto<WallDto>> QueryWallsAsync(WallQueryInput input)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            IQueryable<WallEntity> query = db.Walls.AsNoTracking();

            // 是否包含已删除数据
            if (input.IncludeDeleted)
                query = query.IgnoreQueryFilters();

            // LINQ 筛选条件
            if (!string.IsNullOrWhiteSpace(input.ProjectName))
                query = query.Where(w => w.ProjectName.StartsWith(input.ProjectName));

            if (!string.IsNullOrWhiteSpace(input.WallName))
                query = query.Where(w => w.WallName.Contains(input.WallName));

            if (input.Floor.HasValue)
                query = query.Where(w => w.Floor == input.Floor.Value);

            if (!string.IsNullOrWhiteSpace(input.WallId))
                query = query.Where(w => w.WallId.StartsWith(input.WallId));

            if (input.Statuses is { Count: > 0 })
                query = query.Where(w => input.Statuses.Contains(w.Status));

            if (input.Priorities is { Count: > 0 })
                query = query.Where(w => input.Priorities.Contains(w.Priority));

            if (input.PipelineStages is { Count: > 0 })
                query = query.Where(w => input.PipelineStages.Contains(w.PipelineStage));

            if (input.AuditStatuses is { Count: > 0 })
                query = query.Where(w => input.AuditStatuses.Contains(w.AuditStatus));

            if (input.EndProductionTimeFrom.HasValue)
                query = query.Where(w => w.EndProductionTime >= input.EndProductionTimeFrom.Value);

            if (input.EndProductionTimeTo.HasValue)
                query = query.Where(w => w.EndProductionTime <= input.EndProductionTimeTo.Value.AddDays(1));

            var totalCount = await query.CountAsync();

            // 无数据时提前返回，避免无效排序和分页操作
            if (totalCount == 0)
            {
                return new PagedResultDto<WallDto>
                {
                    TotalCount = 0,
                    Items = new List<WallDto>()
                };
            }

            // 确保分页参数有效
            var page = Math.Max(1, input.Page);
            var pageSize = Math.Max(1, input.PageSize);

            // 排序（去掉 ?? 表达式，避免索引失效）
            query = input.SortField?.ToLower() switch
            {
                "projectname" => input.SortAscending
                    ? query.OrderBy(w => w.ProjectName).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.ProjectName).ThenBy(w => w.Id),
                "wallname" => input.SortAscending
                    ? query.OrderBy(w => w.WallName).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.WallName).ThenBy(w => w.Id),
                "floor" => input.SortAscending
                    ? query.OrderBy(w => w.Floor).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.Floor).ThenBy(w => w.Id),
                "wallid" => input.SortAscending
                    ? query.OrderBy(w => w.WallId).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.WallId).ThenBy(w => w.Id),
                "priority" => input.SortAscending
                    ? query.OrderBy(w => w.Priority).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.Priority).ThenBy(w => w.Id),
                "status" => input.SortAscending
                    ? query.OrderBy(w => w.Status).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.Status).ThenBy(w => w.Id),
                "auditstatus" => input.SortAscending
                    ? query.OrderBy(w => w.AuditStatus).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.AuditStatus).ThenBy(w => w.Id),
                "pipelinestage" => input.SortAscending
                    ? query.OrderBy(w => w.PipelineStage).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.PipelineStage).ThenBy(w => w.Id),
                _ => input.SortAscending
                    ? query.OrderBy(w => w.EndProductionTime).ThenBy(w => w.Id)
                    : query.OrderByDescending(w => w.EndProductionTime).ThenBy(w => w.Id)
            };

            // 投影查询：直接 Select 到 WallDto，绕开 BimJsonData/MomJsonData 两个 MEDIUMTEXT 大字段
            // 同时通过子查询获取 ValidationErrorSummary，避免 Include 集合导航造成的笛卡尔积放大
            var dtos = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new WallDto
                {
                    Id = w.Id,
                    ProjectName = w.ProjectName,
                    WallId = w.WallId,
                    WallName = w.WallName,
                    Floor = w.Floor,
                    PipelineStage = w.PipelineStage,
                    Priority = w.Priority,
                    Status = w.Status,
                    AuditStatus = w.AuditStatus,
                    SchemaVersion = w.SchemaVersion,
                    StartProductionTime = w.StartProductionTime,
                    EndProductionTime = w.EndProductionTime,
                    ImportTime = w.ImportTime,
                    UpdatedAt = w.UpdatedAt,
                    UpdatedBy = w.UpdatedBy,
                    IsDeleted = w.IsDeleted,
                    // 摘要下推到数据库：只取最近一条校验错误，避免 JOIN 大字段
                    ValidationErrorSummary = w.ValidationErrors
                        .OrderByDescending(e => e.CreatedAt)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return new PagedResultDto<WallDto>
            {
                TotalCount = totalCount,
                Items = dtos
            };
        }

        public async Task<WallDetailDto?> GetDetailAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.Walls
                .Include(w => w.Project)
                .Include(w => w.ValidationErrors)
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == wallId);

            if (entity == null)
                return null;

            return _mapper.Map<WallDetailDto>(entity);
        }

        public async Task<WallDetailDto?> GetDetailByWallIdAsync(string wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.Walls
                .Include(w => w.Project)
                .Include(w => w.ValidationErrors)
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WallId == wallId);

            if (entity == null)
                return null;

            return _mapper.Map<WallDetailDto>(entity);
        }

        // ==================== 简单查询 ====================

        public async Task<List<WallDto>> GetByProjectNameAsync(string projectName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entities = await db.Walls
                .Include(w => w.Project)
                .Include(w => w.ValidationErrors)
                .AsNoTracking()
                .Where(w => w.ProjectName == projectName)
                .ToListAsync();

            return _mapper.Map<List<WallDto>>(entities);
        }

        public async Task<List<int>> GetAvailableFloorsAsync(string? projectName = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            IQueryable<WallEntity> query = db.Walls.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(projectName))
                query = query.Where(w => w.ProjectName.Contains(projectName));

            return await query
                .Select(w => w.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();
        }

        // ==================== 存在性/审核检查 ====================

        public async Task<bool> ExistsByWallIdAsync(string wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.Walls.AnyAsync(w => w.WallId == wallId);
        }

        public async Task<HashSet<string>> GetAuditedWallIdsAsync(IEnumerable<string> wallIds)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var ids = await db.Walls
                .Where(w => wallIds.Contains(w.WallId) && w.AuditStatus == (int)AuditStatus.已审核)
                .Select(w => w.WallId)
                .ToListAsync();

            return new HashSet<string>(ids);
        }

        public async Task<HashSet<string>> GetExistingWallIdsAsync(IEnumerable<string> wallIds)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var ids = await db.Walls
                .Where(w => wallIds.Contains(w.WallId))
                .Select(w => w.WallId)
                .ToListAsync();

            return new HashSet<string>(ids);
        }

        // ==================== 新增 ====================

        public async Task InsertManyAsync(List<WallEntity> walls)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            await db.Walls.AddRangeAsync(walls);
            await db.SaveChangesAsync();

            _logger.LogInformation("批量插入 {Count} 面墙体", walls.Count);
        }

        // ==================== 更新（通过领域方法修改实体） ====================

        public async Task UpdatePipelineStageAsync(long wallId, PipelineStage stage)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdatePipelineStage(stage);
            await db.SaveChangesAsync();

            _logger.LogInformation("更新管线阶段: WallId={WallId}, Stage={Stage}", wallId, stage);
        }

        public async Task UpdatePrioritiesAsync(List<long> wallIds, int priority, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var walls = await db.Walls
                .Where(w => wallIds.Contains(w.Id))
                .ToListAsync();

            foreach (var wall in walls)
            {
                wall.UpdatePriority(priority, updatedBy);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("批量更新优先级: {Count}条 → {Priority}", walls.Count, priority);
        }

        public async Task UpdateStatusesAsync(List<long> wallIds, int status, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var walls = await db.Walls
                .Where(w => wallIds.Contains(w.Id))
                .ToListAsync();

            foreach (var wall in walls)
            {
                wall.UpdateStatus(status, updatedBy);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("批量更新状态: {Count}条 → {Status}", walls.Count, status);
        }

        public async Task UpdateJsonDataAsync(long wallId, string? bimJsonData, string? momJsonData, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateJsonData(bimJsonData, momJsonData, updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("手动编辑 JSON: WallId={WallId}", wallId);
        }

        public async Task UpdateWallNameAsync(long wallId, string wallName, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.UpdateWallName(wallName, updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("更新墙体名称: WallId={WallId}, WallName={WallName}", wallId, wallName);
        }

        public async Task SyncBimDataAsync(long wallId, string bimJsonData, string schemaVersion, string wallName, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.SyncBimData(bimJsonData, schemaVersion, wallName, updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("同步更新 BimData: WallId={WallId}, SchemaVer={SchemaVer}", wallId, schemaVersion);
        }

        public async Task SetAuditStatusAsync(long wallId, int auditStatus, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.SetAuditStatus(auditStatus, updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("设置审核状态: WallId={WallId}, AuditStatus={AuditStatus}", wallId, auditStatus);
        }

        public async Task SetAuditStatusBatchAsync(List<long> wallIds, int auditStatus, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var walls = await db.Walls
                .Where(w => wallIds.Contains(w.Id))
                .ToListAsync();

            foreach (var wall in walls)
            {
                wall.SetAuditStatus(auditStatus, updatedBy);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("批量设置审核状态: {Count}条 → AuditStatus={AuditStatus}", walls.Count, auditStatus);
        }

        // ==================== 软删除 ====================

        public async Task SoftDeleteAsync(long wallId, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.SoftDelete(updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("软删除墙体: Id={WallId}", wallId);
        }

        public async Task SoftDeleteManyAsync(List<long> wallIds, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var walls = await db.Walls
                .Where(w => wallIds.Contains(w.Id))
                .ToListAsync();

            foreach (var wall in walls)
            {
                wall.SoftDelete(updatedBy);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("批量软删除墙体: {Count}条", walls.Count);
        }

        public async Task RestoreAsync(long wallId, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var wall = await db.Walls.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == wallId)
                ?? throw new InvalidOperationException($"墙体不存在: Id={wallId}");

            wall.Restore(updatedBy);
            await db.SaveChangesAsync();

            _logger.LogInformation("恢复墙体: Id={WallId}", wallId);
        }

        public async Task RestoreManyAsync(List<long> wallIds, string updatedBy)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var walls = await db.Walls
                .IgnoreQueryFilters()
                .Where(w => wallIds.Contains(w.Id))
                .ToListAsync();

            foreach (var wall in walls)
            {
                wall.Restore(updatedBy);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("批量恢复墙体: {Count}条", walls.Count);
        }

        // ==================== 物理删除 ====================

        public async Task DeleteAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var affected = await db.Walls
                .IgnoreQueryFilters()
                .Where(w => w.Id == wallId)
                .ExecuteDeleteAsync();

            _logger.LogInformation("物理删除墙体: Id={WallId}, Affected={Affected}", wallId, affected);
        }

        public async Task DeleteManyAsync(List<long> wallIds)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var affected = await db.Walls
                .IgnoreQueryFilters()
                .Where(w => wallIds.Contains(w.Id))
                .ExecuteDeleteAsync();

            _logger.LogInformation("批量物理删除墙体: 请求 {Count} 条, 实际删除 {Affected} 条", wallIds.Count, affected);
        }
    }
}