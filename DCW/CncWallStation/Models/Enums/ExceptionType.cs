namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 加工异常类型枚举（主轴异常和通讯异常排前两位）
    /// </summary>
    public enum ExceptionType
    {
        /// <summary>主轴异常</summary>
        主轴异常 = 0,

        /// <summary>PLC 通讯异常</summary>
        通讯异常 = 1,

        /// <summary>刀具断裂</summary>
        刀具断裂 = 2,

        /// <summary>材料缺陷</summary>
        材料缺陷 = 3,

        /// <summary>安全门异常</summary>
        安全门异常 = 4,

        /// <summary>进给异常</summary>
        进给异常 = 5,

        /// <summary>其他异常</summary>
        其他 = 6
    }

    /// <summary>
    /// ExceptionType 扩展方法
    /// </summary>
    public static class ExceptionTypeExtensions
    {
        public static string ToDisplayText(this ExceptionType t) => t switch
        {
            ExceptionType.主轴异常 => "主轴异常",
            ExceptionType.通讯异常 => "PLC通讯异常",
            ExceptionType.刀具断裂 => "刀具断裂",
            ExceptionType.材料缺陷 => "材料缺陷",
            ExceptionType.安全门异常 => "安全门异常",
            ExceptionType.进给异常 => "进给异常",
            ExceptionType.其他 => "其他",
            _ => "未知"
        };

        public static string ToDisplayTextEn(this ExceptionType t) => t switch
        {
            ExceptionType.主轴异常 => "Spindle Error",
            ExceptionType.通讯异常 => "PLC Communication Error",
            ExceptionType.刀具断裂 => "Tool Breakage",
            ExceptionType.材料缺陷 => "Material Defect",
            ExceptionType.安全门异常 => "Safety Door Error",
            ExceptionType.进给异常 => "Feed Error",
            ExceptionType.其他 => "Others",
            _ => "Unknown"
        };
    }
}
