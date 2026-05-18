using AutoMapper;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 项目批次查询应用服务（基于 IDbContextFactory，每个方法独立 DbContext）
    /// </summary>
    public class ProjectAppService : IProjectAppService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<ProjectAppService> _logger;

        public ProjectAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            IMapper mapper,
            ILogger<ProjectAppService> logger)
        {
            _dbFactory = dbFactory;
            _mapper = mapper;
            _logger = logger;
        }

        // ==================== 查询 ====================

        public async Task<List<ProjectDto>> GetLatestProjectsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entities = await db.Projects
                .AsNoTracking()
                .Where(p => p.IsLatest)
                .OrderByDescending(p => p.ImportTime)
                .ToListAsync();

            return _mapper.Map<List<ProjectDto>>(entities);
        }

        // ==================== 新增 ====================

        public async Task<int> CreateProjectAsync(
            string projectNumber,
            string sourceFolderPath,
            string hostName,
            string importedBy,
            int totalWalls)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 获取当前最大版本号
            var maxVersion = await db.Projects
                .Where(p => p.ProjectNumber == projectNumber)
                .MaxAsync(p => (int?)p.Version) ?? 0;

            var project = new ProjectEntity(
                projectNumber,
                maxVersion + 1,
                sourceFolderPath,
                hostName,
                importedBy,
                totalWalls);

            await db.Projects.AddAsync(project);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "创建项目批次: {ProjectNumber} v{Version}, 共 {Total} 面墙",
                projectNumber, project.Version, totalWalls);

            return project.Id;
        }

        public async Task ArchiveOldVersionsAsync(string projectNumber)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var oldProjects = await db.Projects
                .Where(p => p.ProjectNumber == projectNumber && p.IsLatest)
                .ToListAsync();

            if (oldProjects.Count == 0)
                return;

            foreach (var p in oldProjects)
            {
                p.Archive();
            }

            await db.SaveChangesAsync();

            _logger.LogInformation(
                "归档旧版本: {ProjectNumber}, {Count} 个版本",
                projectNumber, oldProjects.Count);
        }
    }
}