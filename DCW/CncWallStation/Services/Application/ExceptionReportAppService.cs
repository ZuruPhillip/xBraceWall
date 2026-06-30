using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 异常报告应用服务实现
    /// </summary>
    public class ExceptionReportAppService : IExceptionReportAppService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<ExceptionReportAppService> _logger;

        public ExceptionReportAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<ExceptionReportAppService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<MachiningExceptionEntity> SaveReportAsync(MachiningExceptionEntity entity)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            db.MachiningExceptions.Add(entity);
            await db.SaveChangesAsync();

            _logger.LogInformation("异常报告已保存: Id={Id}, WallId={WallId}, Type={Type}",
                entity.Id, entity.WallId, entity.ExceptionType);

            return entity;
        }

        /// <inheritdoc/>
        public async Task<List<ExceptionReportDto>> GetReportsByWallIdAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 使用 LEFT JOIN 避免墙体软删除后异常记录无法显示
            return await db.MachiningExceptions
                .AsNoTracking()
                .Where(e => e.WallId == wallId)
                .GroupJoin(db.Walls,
                    e => e.WallId,
                    w => w.Id,
                    (e, walls) => new { e, walls })
                .SelectMany(x => x.walls.DefaultIfEmpty(),
                    (x, w) => ProjectDto(x.e, w))
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task UpdateReportAsync(long reportId, int exceptionType, string? customType, string description, string? photoPaths, DateTime occurredAt, int frequencyCount)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var existing = await db.MachiningExceptions
                .FirstOrDefaultAsync(e => e.Id == reportId);

            if (existing == null)
                throw new InvalidOperationException($"异常报告不存在: Id={reportId}");

            // EF Core 跟踪的实体直接修改属性
            existing.ExceptionType = exceptionType;
            existing.CustomType = customType;
            existing.Description = description;
            existing.PhotoPaths = photoPaths;
            existing.OccurredAt = occurredAt;
            existing.FrequencyCount = frequencyCount;

            await db.SaveChangesAsync();

            _logger.LogInformation("异常报告已更新: Id={Id}", reportId);
        }

        /// <inheritdoc/>
        public async Task ResolveReportAsync(long reportId, string repairMethod, string resolver, decimal? repairDuration, DateTime? completionTime, string? improvementSuggestion, string? remarks)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var report = await db.MachiningExceptions
                .FirstOrDefaultAsync(e => e.Id == reportId);

            if (report == null)
                throw new InvalidOperationException($"异常报告不存在: Id={reportId}");

            report.IsResolved = true;
            report.RepairMethod = repairMethod;
            report.Resolver = resolver;
            report.RepairDuration = repairDuration;
            report.CompletionTime = completionTime;
            report.ImprovementSuggestion = improvementSuggestion;
            report.Remarks = remarks;

            await db.SaveChangesAsync();

            _logger.LogInformation("异常报告已标记解决: Id={Id}, Resolver={Resolver}", reportId, resolver);
        }

        /// <inheritdoc/>
        public async Task<MachiningExceptionEntity?> GetReportAsync(long reportId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.MachiningExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == reportId);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<ExceptionReportDto>> GetPagedReportsAsync(long? wallId, int? exceptionType, DateTime? startDate, DateTime? endDate, bool? isResolved, int pageIndex, int pageSize)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = BuildFilteredQuery(db, wallId, exceptionType, startDate, endDate, isResolved);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.e.OccurredAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(x => ProjectDto(x.e, x.w))
                .ToListAsync();

            _logger.LogInformation("分页查询异常报告: WallId={WallId}, Type={Type}, Start={Start}, End={End}, IsResolved={IsResolved}, Page={Page}/{PageSize}, Total={Total}",
                wallId, exceptionType, startDate, endDate, isResolved, pageIndex, pageSize, totalCount);

            return new PagedResult<ExceptionReportDto>
            {
                TotalCount = totalCount,
                Items = items
            };
        }

        /// <inheritdoc/>
        public async Task<List<ExceptionReportDto>> GetAllReportsForExportAsync(long? wallId, int? exceptionType, DateTime? startDate, DateTime? endDate, bool? isResolved)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = BuildFilteredQuery(db, wallId, exceptionType, startDate, endDate, isResolved);

            var items = await query
                .OrderByDescending(x => x.e.OccurredAt)
                .Select(x => ProjectDto(x.e, x.w))
                .ToListAsync();

            _logger.LogInformation("导出查询异常报告: WallId={WallId}, Type={Type}, Start={Start}, End={End}, IsResolved={IsResolved}, Count={Count}",
                wallId, exceptionType, startDate, endDate, isResolved, items.Count);

            return items;
        }

        /// <summary>
        /// 构建带过滤条件的 LEFT JOIN 查询
        /// </summary>
        private IQueryable<ExceptionWithWall> BuildFilteredQuery(
            AppDbContext db, long? wallId, int? exceptionType, DateTime? startDate, DateTime? endDate, bool? isResolved)
        {
            // 使用 LEFT JOIN 避免墙体软删除后异常记录无法显示
            var query = db.MachiningExceptions
                .AsNoTracking()
                .GroupJoin(db.Walls,
                    e => e.WallId,
                    w => w.Id,
                    (e, walls) => new { e, walls })
                .SelectMany(x => x.walls.DefaultIfEmpty(),
                    (x, w) => new ExceptionWithWall { e = x.e, w = w });

            if (wallId.HasValue && wallId.Value > 0)
            {
                query = query.Where(x => x.e.WallId == wallId.Value);
            }

            if (exceptionType.HasValue)
            {
                int typeValue = exceptionType.Value;
                // 其他(6)类型匹配 CustomType 非空的记录
                if (typeValue == 6)
                {
                    query = query.Where(x => x.e.ExceptionType == 6 || !string.IsNullOrEmpty(x.e.CustomType));
                }
                else
                {
                    query = query.Where(x => x.e.ExceptionType == typeValue);
                }
            }

            if (startDate.HasValue)
            {
                query = query.Where(x => x.e.OccurredAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                // EndDate 含当天（到当天 23:59:59）
                var endExclusive = endDate.Value.Date.AddDays(1);
                query = query.Where(x => x.e.OccurredAt < endExclusive);
            }

            if (isResolved.HasValue)
            {
                bool resolved = isResolved.Value;
                query = query.Where(x => x.e.IsResolved == resolved);
            }

            return query;
        }

        /// <summary>
        /// 实体投影为 DTO
        /// </summary>
        private static ExceptionReportDto ProjectDto(MachiningExceptionEntity e, WallEntity? w)
        {
            return new ExceptionReportDto
            {
                Id = e.Id,
                WallId = e.WallId,
                WallIdStr = w != null ? w.WallId : "(已删除)",
                ExceptionType = e.ExceptionType,
                CustomType = e.CustomType,
                Description = e.Description,
                PhotoPaths = e.PhotoPaths,
                Registrant = e.Registrant,
                CreatedAt = e.CreatedAt,
                OccurredAt = e.OccurredAt,
                FrequencyCount = e.FrequencyCount,
                IsResolved = e.IsResolved,
                RepairMethod = e.RepairMethod,
                Resolver = e.Resolver,
                RepairDuration = e.RepairDuration,
                CompletionTime = e.CompletionTime,
                ImprovementSuggestion = e.ImprovementSuggestion,
                Remarks = e.Remarks
            };
        }

        /// <summary>
        /// LEFT JOIN 中间结果（避免在 EF Core 表达式树中使用元组字面量）
        /// </summary>
        private sealed class ExceptionWithWall
        {
            public MachiningExceptionEntity e { get; set; } = null!;
            public WallEntity? w { get; set; }
        }
    }
}
