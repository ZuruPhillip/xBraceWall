using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 项目批次查询应用服务接口（ABP 风格）
    /// </summary>
    public interface IProjectAppService
    {
        /// <summary>获取所有最新项目列表</summary>
        Task<List<ProjectDto>> GetLatestProjectsAsync();

        /// <summary>创建新导入批次</summary>
        Task<int> CreateProjectAsync(string projectNumber, string sourceFolderPath, string hostName, string importedBy, int totalWalls);

        /// <summary>归档旧版本（同一项目号）</summary>
        Task ArchiveOldVersionsAsync(string projectNumber);
    }
}
