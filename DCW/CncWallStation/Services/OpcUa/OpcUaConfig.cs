namespace CncWallStation.Services.OpcUa;

/// <summary>
/// OPC UA 连接配置模型，与 appsettings.json 中的 OpcUa 节对应
/// </summary>
public class OpcUaConfig
{
    /// <summary>PLC 设备 IP 地址</summary>
    public string IpAddress { get; set; } = "192.168.95.182";

    /// <summary>OPC UA 端口，默认 4840</summary>
    public int Port { get; set; } = 4840;

    /// <summary>初始重连间隔（毫秒）</summary>
    public int ReconnectIntervalMs { get; set; } = 1000;

    /// <summary>最大重连间隔（毫秒）</summary>
    public int MaxReconnectIntervalMs { get; set; } = 30000;

    /// <summary>会话超时时间（毫秒）</summary>
    public int SessionTimeoutMs { get; set; } = 30000;

    /// <summary>KeepAlive 间隔（毫秒）</summary>
    public int KeepAliveIntervalMs { get; set; } = 5000;

    /// <summary>安全策略</summary>
    public string SecurityPolicy { get; set; } = "None";

    /// <summary>是否启动时自动连接</summary>
    public bool AutoConnect { get; set; }

    /// <summary>
    /// 获取 OPC UA 连接 URL
    /// </summary>
    public string GetEndpointUrl()
    {
        return $"opc.tcp://{IpAddress}:{Port}";
    }
}
