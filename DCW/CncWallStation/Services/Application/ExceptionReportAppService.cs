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
                    (x, w) => new ExceptionReportDto
                    {
                        Id = x.e.Id,
                        WallId = x.e.WallId,
                        WallIdStr = w != null ? w.WallId : "(已删除)",
                        ExceptionType = x.e.ExceptionType,
                        CustomType = x.e.CustomType,
                        Description = x.e.Description,
                        PhotoPaths = x.e.PhotoPaths,
                        Operator = x.e.Operator,
                        CreatedAt = x.e.CreatedAt,
                        IsResolved = x.e.IsResolved
                    })
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task UpdateReportAsync(long reportId, int exceptionType, string? customType, string description, string? photoPaths)
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

            await db.SaveChangesAsync();

            _logger.LogInformation("异常报告已更新: Id={Id}", reportId);
        }

        /// <inheritdoc/>
        public async Task ResolveReportAsync(long reportId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var report = await db.MachiningExceptions
                .FirstOrDefaultAsync(e => e.Id == reportId);

            if (report == null)
                throw new InvalidOperationException($"异常报告不存在: Id={reportId}");

            report.IsResolved = true;

            await db.SaveChangesAsync();

            _logger.LogInformation("异常报告已标记解决: Id={reportId}", reportId);
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
        public async Task<PagedResult<ExceptionReportDto>> GetPagedReportsAsync(long? wallId, int pageIndex, int pageSize)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 使用 LEFT JOIN 避免墙体软删除后异常记录无法显示
            var query = db.MachiningExceptions
                .AsNoTracking()
                .GroupJoin(db.Walls,
                    e => e.WallId,
                    w => w.Id,
                    (e, walls) => new { e, walls })
                .SelectMany(x => x.walls.DefaultIfEmpty(),
                    (x, w) => new { x.e, w });

            if (wallId.HasValue && wallId.Value > 0)
            {
                query = query.Where(x => x.e.WallId == wallId.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.e.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(x => new ExceptionReportDto
                {
                    Id = x.e.Id,
                    WallId = x.e.WallId,
                    WallIdStr = x.w != null ? x.w.WallId : "(已删除)",
                    ExceptionType = x.e.ExceptionType,
                    CustomType = x.e.CustomType,
                    Description = x.e.Description,
                    PhotoPaths = x.e.PhotoPaths,
                    Operator = x.e.Operator,
                    CreatedAt = x.e.CreatedAt,
                    IsResolved = x.e.IsResolved
                })
                .ToListAsync();

            _logger.LogInformation("分页查询异常报告: WallId={WallId}, Page={Page}/{PageSize}, Total={Total}",
                wallId, pageIndex, pageSize, totalCount);

            return new PagedResult<ExceptionReportDto>
            {
                TotalCount = totalCount,
                Items = items
            };
        }
    }
}
