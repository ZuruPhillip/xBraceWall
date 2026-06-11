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
        /// Handler → 特征名称 映射字典（中文）
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
            { "StepHandler", "钢柱槽" },
            { "GlueSealHandler", "密封条" },
            { "XBraceHandler", "X斜槽" },
            { "TopPlateHandler", "顶板" }
        };

        /// <summary>
        /// Handler → 特征名称 映射字典（英文）
        /// </summary>
        public static readonly Dictionary<string, string> FeatureNameMapEn = new()
        {
            { "WallHandler", "Wall Definition" },
            { "BoxHandler", "Switch Box" },
            { "HoleHandler", "Hole" },
            { "BendingKeyHandler", "Bending Key" },
            { "RebarSlotHandler", "Rebar Slot" },
            { "WindowHandler", "Window" },
            { "CableHandler", "MEP Cable Slot" },
            { "ProppingHandler", "Propping" },
            { "StepHandler", "Column Steel Groove" },
            { "GlueSealHandler", "Glue Seal" },
            { "XBraceHandler", "X-Brace" },
            { "TopPlateHandler", "Top Plate" }
        };

        /// <summary>该分组下的指令列表</summary>
        public List<PlcInstruction> Instructions { get; set; } = new();
    }
}
