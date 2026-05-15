using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;

namespace CncWallStation.Repositories
{
    /// <summary>
    /// 墙体数据仓储接口
    /// </summary>
    public interface IWallRepository
    {
        // ==================== 导入 ====================

        /// <summary>创建新的导入批次（Project），返回 ProjectId</summary>
        Task<int> CreateProjectAsync(string projectNumber, string sourceFolderPath, string hostName, string importedBy, int totalWalls);

        /// <summary>同一项目号再次导入时，将旧版本标记为非最新</summary>
        Task ArchiveOldVersionsAsync(string projectNumber);

        /// <summary>批量插入墙体数据（PipelineStage = Imported）</summary>
        Task AddWallsAsync(List<WallEntity> walls);

        // ==================== 查询 ====================

        /// <summary>分页查询墙体数据，支持多条件组合筛选排序</summary>
        Task<(List<WallEntity> Items, int TotalCount)> QueryWallsAsync(
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
            bool latestOnly = true);

        /// <summary>按 ID 获取单条墙体（含 ValidationErrors 导航）</summary>
        Task<WallEntity?> GetWallByIdAsync(long wallId);

        /// <summary>获取项目的可用楼层列表</summary>
        Task<List<int>> GetAvailableFloorsAsync(string? projectNumber = null);

        /// <summary>获取所有最新项目列表</summary>
        Task<List<ProjectEntity>> GetLatestProjectsAsync();

        // ==================== 更新 ====================

        /// <summary>更新墙体管线阶段</summary>
        Task UpdatePipelineStageAsync(long wallId, PipelineStage stage);

        /// <summary>更新墙体 MomJsonData</summary>
        Task UpdateMomJsonDataAsync(long wallId, string momJsonData);

        /// <summary>更新墙体优先级</summary>
        Task UpdatePriorityAsync(long wallId, int priority, string updatedBy);

        /// <summary>更新墙体状态</summary>
        Task UpdateStatusAsync(long wallId, int status, string updatedBy);

        /// <summary>手动编辑 BimJsonData 或 MomJsonData（异常状态下）</summary>
        Task UpdateJsonDataAsync(long wallId, string? bimJsonData, string? momJsonData, string updatedBy);

        // ==================== 删除 ====================

        /// <summary>删除指定墙体</summary>
        Task DeleteWallAsync(long wallId);

        // ==================== 校验错误 ====================

        /// <summary>添加校验/转换错误记录</summary>
        Task AddValidationErrorsAsync(List<ValidationErrorEntity> errors);

        /// <summary>按 GroupId 获取错误记录</summary>
        Task<List<ValidationErrorEntity>> GetValidationErrorsByGroupIdAsync(string groupId);

        /// <summary>按 WallId 获取错误记录</summary>
        Task<List<ValidationErrorEntity>> GetValidationErrorsByWallIdAsync(long wallId);

        // ==================== 事务 ====================

        /// <summary>保存所有更改</summary>
        Task<int> SaveChangesAsync();
    }
}
