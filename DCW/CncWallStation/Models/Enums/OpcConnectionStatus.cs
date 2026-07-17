namespace CncWallStation.Models.Enums;

/// <summary>
/// OPC UA 连接状态枚举
/// </summary>
public enum OpcConnectionStatus
{
    /// <summary>已断开</summary>
    Disconnected,
    /// <summary>连接中</summary>
    Connecting,
    /// <summary>已连接</summary>
    Connected,
    /// <summary>连接异常</summary>
    Error
}
