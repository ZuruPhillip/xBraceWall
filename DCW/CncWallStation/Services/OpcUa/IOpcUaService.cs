using CncWallStation.Models.Enums;
using Opc.Ua;

namespace CncWallStation.Services.OpcUa;

/// <summary>
/// OPC UA 通讯服务接口，定义连接管理、批量读写、节点订阅等核心方法
/// </summary>
public interface IOpcUaService
{
    /// <summary>当前连接状态</summary>
    OpcConnectionStatus Status { get; }

    /// <summary>是否已连接</summary>
    bool IsConnected { get; }

    /// <summary>连接状态变更事件</summary>
    event EventHandler<OpcConnectionStatus> StatusChanged;

    /// <summary>节点值更新事件（订阅回调触发）</summary>
    event EventHandler<IReadOnlyList<OpcNodeConfig>> NodeValuesUpdated;

    /// <summary>
    /// 连接到 OPC UA 服务器
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 批量读取节点值
    /// </summary>
    /// <param name="nodeIds">要读取的 NodeId 列表</param>
    /// <returns>读取到的 DataValue 列表</returns>
    Task<IReadOnlyList<DataValue>> ReadNodesAsync(IEnumerable<string> nodeIds, CancellationToken ct = default);

    /// <summary>
    /// 批量写入节点值
    /// </summary>
    /// <param name="nodeValues">NodeId -> 值的字典</param>
    Task WriteNodesAsync(IReadOnlyDictionary<string, object> nodeValues, CancellationToken ct = default);

    /// <summary>
    /// 订阅指定节点，实时接收值变更通知
    /// </summary>
    Task SubscribeNodesAsync(IEnumerable<OpcNodeConfig> nodes, CancellationToken ct = default);

    /// <summary>
    /// 取消所有订阅
    /// </summary>
    void UnsubscribeAll();

    /// <summary>
    /// 获取当前已订阅的节点列表
    /// </summary>
    Task<IReadOnlyList<OpcNodeConfig>> GetSubscribedNodesAsync();

    /// <summary>
    /// 健康检查（快速检测连接是否存活）
    /// </summary>
    Task<bool> HealthCheckAsync();

    /// <summary>
    /// 加载并应用最新配置（从 appsettings.json 读取）
    /// </summary>
    void ReloadConfig();

    /// <summary>
    /// 获取当前配置
    /// </summary>
    OpcUaConfig GetCurrentConfig();
}
