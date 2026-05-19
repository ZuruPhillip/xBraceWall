namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 校验错误严重等级
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>提示：可忽略，仅供参考</summary>
        Info = 0,

        /// <summary>警告：需操作员确认</summary>
        Warning = 1,

        /// <summary>错误：阻断加工</summary>
        Error = 2,

        /// <summary>致命：直接拒绝下发</summary>
        Critical = 3
    }

    /// <summary>
    /// ErrorSeverity 扩展方法
    /// </summary>
    public static class ErrorSeverityExtensions
    {
        /// <summary>获取严重等级中文显示文本</summary>
        public static string ToDisplayText(this ErrorSeverity severity) => severity switch
        {
            ErrorSeverity.Info => "提示",
            ErrorSeverity.Warning => "警告",
            ErrorSeverity.Error => "错误",
            ErrorSeverity.Critical => "致命",
            _ => "未知"
        };

        /// <summary>获取严重等级英文显示文本</summary>
        public static string ToDisplayTextEn(this ErrorSeverity severity) => severity switch
        {
            ErrorSeverity.Info => "Info",
            ErrorSeverity.Warning => "Warning",
            ErrorSeverity.Error => "Error",
            ErrorSeverity.Critical => "Critical",
            _ => "Unknown"
        };

        /// <summary>获取严重等级对应的颜色十六进制（用于 UI 绑定）</summary>
        public static string ToColorHex(this ErrorSeverity severity) => severity switch
        {
            ErrorSeverity.Info => "#1677FF",
            ErrorSeverity.Warning => "#FAAD14",
            ErrorSeverity.Error => "#FF7A45",
            ErrorSeverity.Critical => "#FF4D4F",
            _ => "#999999"
        };

        /// <summary>获取严重等级评分权重</summary>
        public static int GetWeight(this ErrorSeverity severity) => severity switch
        {
            ErrorSeverity.Info => 1,
            ErrorSeverity.Warning => 5,
            ErrorSeverity.Error => 15,
            ErrorSeverity.Critical => 40,
            _ => 0
        };
    }
}
