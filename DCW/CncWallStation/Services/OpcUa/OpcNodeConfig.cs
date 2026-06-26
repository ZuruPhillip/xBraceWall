using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.Services.OpcUa;

/// <summary>
/// OPC UA 节点配置模型，描述一个需要读写或订阅的节点
/// </summary>
public partial class OpcNodeConfig : ObservableObject
{
    /// <summary>节点唯一标识符，如 ns=3;s="MyVariable"</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>节点描述/别名</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否可写</summary>
    public bool IsWritable { get; set; }

    /// <summary>
    /// 当前值（订阅或读取后缓存的最新值）
    /// </summary>
    [ObservableProperty]
    private object? _currentValue;

    /// <summary>最后一次更新时间</summary>
    [ObservableProperty]
    private DateTime? _lastUpdated;

    /// <summary>数据质量状态：Good / Bad / Uncertain</summary>
    [ObservableProperty]
    private string _quality = string.Empty;

    /// <summary>源时间戳（来自 OPC 服务器）</summary>
    [ObservableProperty]
    private DateTime? _sourceTimestamp;
}
