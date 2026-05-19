namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 校验错误分类（BimError / MomError）
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>BimData 校验错误</summary>
        Bim = 0,

        /// <summary>MomData 校验错误</summary>
        Mom = 1
    }

    public static class ErrorCategoryExtensions
    {
        public static string ToDisplayText(this ErrorCategory category) => category switch
        {
            ErrorCategory.Bim => "BimError",
            ErrorCategory.Mom => "MomError",
            _ => "Unknown"
        };
    }
}
