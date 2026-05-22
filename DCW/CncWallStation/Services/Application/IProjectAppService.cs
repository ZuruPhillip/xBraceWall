using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 项目批次查询应用服务接口（ABP 风格）
    /// </summary>
    public interface IProjectAppService
    {
        /// <summary>获取所有项目列表</summary>
        Task<List<ProjectDto>> GetAllProjectsAsync();

        /// <summary>创建新导入批次</summary>
        Task<int> CreateProjectAsync(string projectName, string sourceFolderPath, string hostName, string importedBy, int totalWalls);
    }
}
