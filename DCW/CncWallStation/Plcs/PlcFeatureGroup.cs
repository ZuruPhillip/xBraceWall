namespace CncWallStation.Plcs
{
    /// <summary>
    /// PLC 特征分组：按 Handler 将指令集归类
    /// </summary>
    public class PlcFeatureGroup
    {
        /// <summary>Handler 类名（如 "WallHandler"）</summary>
        public string HandlerName { get; set; } = string.Empty;

        /// <summary>特征中文名称（如 "墙定义"、"开关盒"）</summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// Handler → 特征名称 映射字典
        /// </summary>
        public static readonly Dictionary<string, string> FeatureNameMap = new()
        {
            { "WallHandler", "墙定义" },
            { "BoxHandler", "开关盒" },
            { "HoleHandler", "普通孔" },
            { "BendingKeyHandler", "定位孔" },
            { "RebarSlotHandler", "钢筋槽" },
            { "WindowHandler", "窗户" },
            { "CableHandler", "电缆槽" },
            { "ProppingHandler", "斜撑" },
            { "StepHandler", "台阶" },
            { "GlueSealHandler", "密封条" },
            { "XBraceHandler", "X斜槽" }
        };

        /// <summary>该分组下的指令列表</summary>
        public List<PlcInstruction> Instructions { get; set; } = new();
    }
}
