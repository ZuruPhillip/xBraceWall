namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 审核状态枚举
    /// </summary>
    public enum AuditStatus
    {
        /// <summary>未审核（可导入/可编辑）</summary>
        未审核 = 0,

        /// <summary>已审核（锁定，不可覆盖导入）</summary>
        已审核 = 1
    }

    /// <summary>
    /// AuditStatus 扩展方法
    /// </summary>
    public static class AuditStatusExtensions
    {
        /// <summary>获取审核状态的中文显示文本</summary>
        public static string ToDisplayText(this AuditStatus status) => status switch
        {
            AuditStatus.未审核 => "未审核",
            AuditStatus.已审核 => "已审核",
            _ => "未知"
        };

        /// <summary>从 int 转换为 AuditStatus</summary>
        public static AuditStatus FromInt(int value) => value switch
        {
            1 => AuditStatus.已审核,
            _ => AuditStatus.未审核
        };
    }
}
