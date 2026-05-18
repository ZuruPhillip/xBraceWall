using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 墙体查询应用服务接口（ABP 风格）
    /// </summary>
    public interface IWallAppService
    {
        // ==================== 查询 ====================

        /// <summary>分页查询墙体列表（复杂查询）</summary>
        Task<PagedResultDto<WallDto>> QueryWallsAsync(WallQueryInput input);

        /// <summary>按 ID 获取墙体详情（含完整数据 + 导航属性）</summary>
        Task<WallDetailDto?> GetDetailAsync(long wallId);

        /// <summary>按项目号获取墙体列表（简单查询）</summary>
        Task<List<WallDto>> GetByProjectNumberAsync(string projectNumber);

        /// <summary>获取可用楼层列表</summary>
        Task<List<int>> GetAvailableFloorsAsync(string? projectNumber = null);

        // ==================== 新增 ====================

        /// <summary>批量插入墙体（InsertManyAsync）</summary>
        Task InsertManyAsync(List<WallEntity> walls);

        // ==================== 更新 ====================

        /// <summary>更新管线阶段</summary>
        Task UpdatePipelineStageAsync(long wallId, PipelineStage stage);

        /// <summary>更新优先级（批量）</summary>
        Task UpdatePrioritiesAsync(List<long> wallIds, int priority, string updatedBy);

        /// <summary>更新状态（批量）</summary>
        Task UpdateStatusesAsync(List<long> wallIds, int status, string updatedBy);

        /// <summary>手动编辑 JSON 数据</summary>
        Task UpdateJsonDataAsync(long wallId, string? bimJsonData, string? momJsonData, string updatedBy);

        // ==================== 删除 ====================

        /// <summary>直接删除墙体（DeleteDirectAsync，不加载实体）</summary>
        Task DeleteAsync(long wallId);

        /// <summary>批量直接删除墙体</summary>
        Task DeleteManyAsync(List<long> wallIds);
    }
}
