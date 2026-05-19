namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 预检结果
    /// </summary>
    public enum CheckResult
    {
        /// <summary>预检通过</summary>
        Pass = 0,

        /// <summary>预检失败</summary>
        Fail = 1,

        /// <summary>人工放行（Override）</summary>
        Override = 2
    }

    public static class CheckResultExtensions
    {
        public static string ToDisplayText(this CheckResult result) => result switch
        {
            CheckResult.Pass => "通过",
            CheckResult.Fail => "失败",
            CheckResult.Override => "人工放行",
            _ => "未知"
        };

        public static string ToDisplayTextEn(this CheckResult result) => result switch
        {
            CheckResult.Pass => "Pass",
            CheckResult.Fail => "Fail",
            CheckResult.Override => "Override",
            _ => "Unknown"
        };
    }
}
