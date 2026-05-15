using CncWallStation.Data;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Repositories
{
    /// <summary>
    /// 墙体数据仓储实现
    /// </summary>
    public class WallRepository : IWallRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WallRepository> _logger;

        public WallRepository(AppDbContext db, ILogger<WallRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ==================== 导入 ====================

        public async Task<int> CreateProjectAsync(string projectNumber, string sourceFolderPath, string hostName, string importedBy, int totalWalls)
        {
            // 获取当前最大版本号
            var maxVersion = await _db.Projects
                .Where(p => p.ProjectNumber == projectNumber)
                .MaxAsync(p => (int?)p.Version) ?? 0;

            var project = new ProjectEntity
            {
                ProjectNumber = projectNumber,
                Version = maxVersion + 1,
                IsLatest = true,
                SourceFolderPath = sourceFolderPath,
                HostName = hostName,
                ImportedBy = importedBy,
                TotalWalls = totalWalls,
                ImportTime = DateTime.Now
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();
            _logger.LogInformation("创建项目批次: {ProjectNumber} v{Version}, 共 {Total} 面墙",
                projectNumber, project.Version, totalWalls);

            return project.Id;
        }

        public async Task ArchiveOldVersionsAsync(string projectNumber)
        {
            var oldProjects = await _db.Projects
                .Where(p => p.ProjectNumber == projectNumber && p.IsLatest)
                .ToListAsync();

            foreach (var p in oldProjects)
            {
                p.IsLatest = false;
            }

            if (oldProjects.Count > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("归档旧版本: {ProjectNumber}, {Count} 个版本", projectNumber, oldProjects.Count);
            }
        }

        public async Task AddWallsAsync(List<WallEntity> walls)
        {
            await _db.Walls.AddRangeAsync(walls);
            await _db.SaveChangesAsync();
            _logger.LogInformation("批量插入 {Count} 面墙体", walls.Count);
        }

        // ==================== 查询 ====================

        public async Task<(List<WallEntity> Items, int TotalCount)> QueryWallsAsync(
            string? projectNumber = null,
            int? floor = null,
            string? wallId = null,
            List<int>? statuses = null,
            List<int>? priorities = null,
            List<PipelineStage>? pipelineStages = null,
            DateTime? importTimeFrom = null,
            DateTime? importTimeTo = null,
            string? sortField = null,
            bool sortAscending = true,
            int page = 1,
            int pageSize = 20,
            bool latestOnly = true)
        {
            var query = _db.Walls.AsQueryable();

            // 默认只查最新版本
            if (latestOnly)
            {
                var latestProjectIds = await _db.Projects
                    .Where(p => p.IsLatest)
                    .Select(p => p.Id)
                    .ToListAsync();
                query = query.Where(w => latestProjectIds.Contains(w.ProjectId));
            }

            // 筛选
            if (!string.IsNullOrWhiteSpace(projectNumber))
                query = query.Where(w => w.ProjectNumber.Contains(projectNumber));

            if (floor.HasValue)
                query = query.Where(w => w.Floor == floor.Value);

            if (!string.IsNullOrWhiteSpace(wallId))
                query = query.Where(w => w.WallId.Contains(wallId));

            if (statuses != null && statuses.Count > 0)
                query = query.Where(w => statuses.Contains(w.Status));

            if (priorities != null && priorities.Count > 0)
                query = query.Where(w => priorities.Contains(w.Priority));

            if (pipelineStages != null && pipelineStages.Count > 0)
                query = query.Where(w => pipelineStages.Contains(w.PipelineStage));

            if (importTimeFrom.HasValue)
                query = query.Where(w => w.ImportTime >= importTimeFrom.Value);

            if (importTimeTo.HasValue)
                query = query.Where(w => w.ImportTime <= importTimeTo.Value.AddDays(1));

            // 总数
            var totalCount = await query.CountAsync();

            // 排序
            query = sortField?.ToLower() switch
            {
                "projectnumber" => sortAscending
                    ? query.OrderBy(w => w.ProjectNumber)
                    : query.OrderByDescending(w => w.ProjectNumber),
                "floor" => sortAscending
                    ? query.OrderBy(w => w.Floor)
                    : query.OrderByDescending(w => w.Floor),
                "wallid" => sortAscending
                    ? query.OrderBy(w => w.WallId)
                    : query.OrderByDescending(w => w.WallId),
                "priority" => sortAscending
                    ? query.OrderBy(w => w.Priority)
                    : query.OrderByDescending(w => w.Priority),
                "status" => sortAscending
                    ? query.OrderBy(w => w.Status)
                    : query.OrderByDescending(w => w.Status),
                "pipelinestage" => sortAscending
                    ? query.OrderBy(w => w.PipelineStage)
                    : query.OrderByDescending(w => w.PipelineStage),
                _ => sortAscending
                    ? query.OrderByDescending(w => w.ImportTime)
                    : query.OrderBy(w => w.ImportTime)
            };

            // 分页
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(w => w.ValidationErrors)
                .AsNoTracking()
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<WallEntity?> GetWallByIdAsync(long wallId)
        {
            return await _db.Walls
                .Include(w => w.ValidationErrors)
                .FirstOrDefaultAsync(w => w.Id == wallId);
        }

        public async Task<List<int>> GetAvailableFloorsAsync(string? projectNumber = null)
        {
            var query = _db.Walls.AsQueryable();

            if (!string.IsNullOrWhiteSpace(projectNumber))
                query = query.Where(w => w.ProjectNumber.Contains(projectNumber));

            return await query
                .Select(w => w.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();
        }

        public async Task<List<ProjectEntity>> GetLatestProjectsAsync()
        {
            return await _db.Projects
                .Where(p => p.IsLatest)
                .OrderByDescending(p => p.ImportTime)
                .AsNoTracking()
                .ToListAsync();
        }

        // ==================== 更新 ====================

        public async Task UpdatePipelineStageAsync(long wallId, PipelineStage stage)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体 {wallId} 不存在");

            wall.PipelineStage = stage;
            wall.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        public async Task UpdateMomJsonDataAsync(long wallId, string momJsonData)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体 {wallId} 不存在");

            wall.MomJsonData = momJsonData;
            wall.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        public async Task UpdatePriorityAsync(long wallId, int priority, string updatedBy)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体 {wallId} 不存在");

            wall.Priority = priority;
            wall.UpdatedBy = updatedBy;
            wall.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(long wallId, int status, string updatedBy)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体 {wallId} 不存在");

            wall.Status = status;
            wall.UpdatedBy = updatedBy;
            wall.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        public async Task UpdateJsonDataAsync(long wallId, string? bimJsonData, string? momJsonData, string updatedBy)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall == null)
                throw new InvalidOperationException($"墙体 {wallId} 不存在");

            if (bimJsonData != null)
                wall.BimJsonData = bimJsonData;
            if (momJsonData != null)
                wall.MomJsonData = momJsonData;

            wall.UpdatedBy = updatedBy;
            wall.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        // ==================== 删除 ====================

        public async Task DeleteWallAsync(long wallId)
        {
            var wall = await _db.Walls.FindAsync(wallId);
            if (wall != null)
            {
                _db.Walls.Remove(wall);
                await _db.SaveChangesAsync();
                _logger.LogInformation("删除墙体: {WallId} (ID={Id})", wall.WallId, wallId);
            }
        }

        // ==================== 校验错误 ====================

        public async Task AddValidationErrorsAsync(List<ValidationErrorEntity> errors)
        {
            await _db.ValidationErrors.AddRangeAsync(errors);
            await _db.SaveChangesAsync();
            _logger.LogInformation("记录 {Count} 条校验错误 (GroupId={GroupId})",
                errors.Count, errors.FirstOrDefault()?.GroupId);
        }

        public async Task<List<ValidationErrorEntity>> GetValidationErrorsByGroupIdAsync(string groupId)
        {
            return await _db.ValidationErrors
                .Where(e => e.GroupId == groupId)
                .OrderBy(e => e.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<ValidationErrorEntity>> GetValidationErrorsByWallIdAsync(long wallId)
        {
            return await _db.ValidationErrors
                .Where(e => e.WallId == wallId)
                .OrderByDescending(e => e.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        // ==================== 事务 ====================

        public async Task<int> SaveChangesAsync()
        {
            return await _db.SaveChangesAsync();
        }
    }
}
