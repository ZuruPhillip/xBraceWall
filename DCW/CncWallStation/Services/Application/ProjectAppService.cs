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

        public async Task<List<ProjectDto>> GetAllProjectsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entities = await db.Projects
                .AsNoTracking()
                .OrderByDescending(p => p.ImportTime)
                .ToListAsync();

            return _mapper.Map<List<ProjectDto>>(entities);
        }

        // ==================== 新增 ====================

        public async Task<int> CreateProjectAsync(
            string projectName,
            string sourceFolderPath,
            string hostName,
            string importedBy,
            int totalWalls)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var project = new ProjectEntity(
                projectName,
                sourceFolderPath,
                hostName,
                importedBy,
                totalWalls);

            await db.Projects.AddAsync(project);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "创建项目批次: {ProjectName}, 共 {Total} 面墙",
                projectName, totalWalls);

            return project.Id;
        }
    }
}