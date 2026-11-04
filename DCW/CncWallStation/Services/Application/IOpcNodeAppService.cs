using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// OPC 节点配置应用服务：分组与节点的增删改查、Excel 导入落库、旧配置迁移
    /// </summary>
    public interface IOpcNodeAppService
    {
        // ==================== 分组 ====================

        /// <summary>获取全部（带节点数量）</summary>
        Task<List<OpcNodeGroupDto>> GetGroupsAsync();

        /// <summary>新建分组，重名时抛出 <see cref="InvalidOperationException"/></summary>
        Task<OpcNodeGroupDto> CreateGroupAsync(string name, string? description);

        /// <summary>重命名分组，重名时抛出 <see cref="InvalidOperationException"/></summary>
        Task RenameGroupAsync(int groupId, string name, string? description);

        /// <summary>删除分组，其下节点自动移入“未分组”</summary>
        /// <returns>被移至未分组的节点数量</returns>
        Task<int> DeleteGroupAsync(int groupId);

        // ==================== 节点 ====================

        /// <summary>
        /// 查询节点列表
        /// </summary>
        /// <param name="groupId">分组过滤（null 表示不过滤）</param>
        /// <param name="ungroupedOnly">仅未分组节点（优先级高于 groupId）</param>
        /// <param name="keyword">NodeId / 描述模糊匹配</param>
        Task<List<OpcNodeDto>> GetNodesAsync(int? groupId = null, string? keyword = null, bool ungroupedOnly = false);

        /// <summary>获取勾选了“实时显示”的节点（控制页实时参数面板使用）</summary>
        Task<List<OpcNodeDto>> GetRealtimeNodesAsync();

        /// <summary>新建节点，NodeId 重复时抛出 <see cref="InvalidOperationException"/></summary>
        Task<OpcNodeDto> CreateNodeAsync(OpcNodeDto dto);

        /// <summary>更新节点（NodeId 不可修改），节点不存在时抛出 <see cref="InvalidOperationException"/></summary>
        Task UpdateNodeAsync(OpcNodeDto dto);

        /// <summary>批量删除节点</summary>
        /// <returns>实际删除条数</returns>
        Task<int> DeleteNodesAsync(IEnumerable<long> ids);

        /// <summary>判断 NodeId 是否已存在</summary>
        Task<bool> NodeIdExistsAsync(string nodeId, long excludeId = 0);

        /// <summary>按 NodeId 合并导入（存在则更新，不存在则新增；分组名不存在时自动创建）</summary>
        Task<OpcNodeImportResult> ImportAsync(IReadOnlyList<OpcNodeImportRow> rows);

        /// <summary>
        /// 旧版 opc_nodes.json → 数据库的一次性迁移。
        /// 仅当节点表为空且文件存在时执行，成功后把文件重命名为 .bak。
        /// </summary>
        /// <returns>迁移的节点数量（0 表示未执行）</returns>
        Task<int> MigrateLegacyJsonIfEmptyAsync(string legacyJsonPath);
    }
}
