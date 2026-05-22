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

        /// <summary>按 WallId（字符串）获取墙体详情</summary>
        Task<WallDetailDto?> GetDetailByWallIdAsync(string wallId);

        /// <summary>按项目名称获取墙体列表（简单查询）</summary>
        Task<List<WallDto>> GetByProjectNameAsync(string projectName);

        /// <summary>获取可用楼层列表</summary>
        Task<List<int>> GetAvailableFloorsAsync(string? projectName = null);

        // ==================== 存在性/审核检查 ====================

        /// <summary>检查 WallId 是否已存在（排除软删除）</summary>
        Task<bool> ExistsByWallIdAsync(string wallId);

        /// <summary>检查 WallId 是否已审核（返回审核的墙体ID集合）</summary>
        Task<HashSet<string>> GetAuditedWallIdsAsync(IEnumerable<string> wallIds);

        /// <summary>获取已存在的 WallId 集合（排除软删除）</summary>
        Task<HashSet<string>> GetExistingWallIdsAsync(IEnumerable<string> wallIds);

        // ==================== 新增 ====================

        /// <summary>批量插入墙体</summary>
        Task InsertManyAsync(List<WallEntity> walls);

        // ==================== 更新 ====================

        /// <summary>更新管线阶段</summary>
        Task UpdatePipelineStageAsync(long wallId, PipelineStage stage);

        /// <summary>更新优先级（批量）</summary>
        Task UpdatePrioritiesAsync(List<long> wallIds, int priority, string updatedBy);

        /// <summary>更新生产状态（批量）</summary>
        Task UpdateStatusesAsync(List<long> wallIds, int status, string updatedBy);

        /// <summary>手动编辑 JSON 数据</summary>
        Task UpdateJsonDataAsync(long wallId, string? bimJsonData, string? momJsonData, string updatedBy);

        /// <summary>更新墙体名称</summary>
        Task UpdateWallNameAsync(long wallId, string wallName, string updatedBy);

        /// <summary>同步更新 BimData（仅未审核墙体，替换BimJson+清空MomData+重置管线）</summary>
        Task SyncBimDataAsync(long wallId, string bimJsonData, string schemaVersion, string wallName, string updatedBy);

        /// <summary>设置审核状态（单条）</summary>
        Task SetAuditStatusAsync(long wallId, int auditStatus, string updatedBy);

        /// <summary>批量设置审核状态</summary>
        Task SetAuditStatusBatchAsync(List<long> wallIds, int auditStatus, string updatedBy);

        // ==================== 删除 ====================

        /// <summary>软删除墙体（IsDeleted=true）</summary>
        Task SoftDeleteAsync(long wallId, string updatedBy);

        /// <summary>批量软删除墙体</summary>
        Task SoftDeleteManyAsync(List<long> wallIds, string updatedBy);

        /// <summary>恢复已删除墙体</summary>
        Task RestoreAsync(long wallId, string updatedBy);

        /// <summary>批量恢复已删除墙体</summary>
        Task RestoreManyAsync(List<long> wallIds, string updatedBy);

        /// <summary>物理删除墙体（硬删除，谨慎使用）</summary>
        Task DeleteAsync(long wallId);

        /// <summary>批量物理删除墙体</summary>
        Task DeleteManyAsync(List<long> wallIds);
    }
}
