namespace CncWallStation.Features
{
    /// <summary>特征类型</summary>
    public enum FeatureType
    {
        Groove,      // 矩形切槽
        Hole,        // 开孔(圆孔/腰孔)
        Pocket,      // 矩形挖坑
        MepSlot,     // 电线管道线槽
        RebarSlot,   // 钢筋槽
        Propping,    // 斜撑
        Window,      // 窗口
        PolygonCut   // 多边形切割（扩展用）
    }

    /// <summary>标准加工面（六面体六个面）</summary>
    public enum MachineSide
    {
        Top,     // +Z 顶面
        Bottom,  // -Z 底面
        Front,   // +Y 前面
        Back,    // -Y 后面
        Right,   // +X 右面
        Left,    // -X 左面
        Custom   // 自定义法向量
    }

    /// <summary>翻面轴方向</summary>
    public enum FlipAxis
    {
        /// <summary>绕X轴翻面：Top↔Bottom，Front↔Back</summary>
        AroundX,

        /// <summary>绕Y轴翻面：Top↔Bottom，Left↔Right</summary>
        AroundY,

        /// <summary>绕Z轴翻面：Front↔Back，Left↔Right</summary>
        AroundZ
    }

    /// <summary>
    /// 槽类型枚举
    /// 描述槽的业务用途，便于后续加工路径分类处理
    /// </summary>
    public enum GrooveType
    {
        /// <summary>通用槽（未分类）</summary>
        General,

        /// <summary>钢柱槽（steelColumnGroove）</summary>
        SteelColumn,

        /// <summary>斜撑钢槽（xBraceSteelGroove）</summary>
        XBraceSteel,

        /// <summary>钢柱底板槽</summary>
        BaseBracket,

        /// <summary>钢柱顶板槽</summary>
        TopBracket,

        /// <summary>顶板槽</summary>
        TopPlate,

        /// <summary>胶水密封槽</summary>
        GlueSeal,

        /// <summary>可扩展自定义类型</summary>
        Custom
    }

    /// <summary>孔形状类型</summary>
    public enum HoleShape
    {
        /// <summary>圆形孔</summary>
        Round,

        /// <summary>腰孔（长圆孔/槽孔）</summary>
        Slotted
    }

    /// <summary>FeatureType 扩展方法</summary>
    public static class FeatureTypeExtensions
    {
        public static string ToDisplayText(this FeatureType ft) => ft switch
        {
            FeatureType.Groove => "矩形切槽",
            FeatureType.Hole => "开孔",
            FeatureType.Pocket => "矩形挖坑",
            FeatureType.MepSlot => "电线管道线槽",
            FeatureType.RebarSlot => "钢筋槽",
            FeatureType.Propping => "斜撑",
            FeatureType.Window => "窗口",
            FeatureType.PolygonCut => "多边形切割",
            _ => ft.ToString()
        };

        public static string ToDisplayTextEn(this FeatureType ft) => ft switch
        {
            FeatureType.Groove => "Groove",
            FeatureType.Hole => "Hole",
            FeatureType.Pocket => "Pocket",
            FeatureType.MepSlot => "MEP Slot",
            FeatureType.RebarSlot => "Rebar Slot",
            FeatureType.Propping => "Propping",
            FeatureType.Window => "Window",
            FeatureType.PolygonCut => "Polygon Cut",
            _ => ft.ToString()
        };
    }

    /// <summary>MachineSide 扩展方法</summary>
    public static class MachineSideExtensions
    {
        public static string ToDisplayText(this MachineSide ms) => ms switch
        {
            MachineSide.Top => "顶面",
            MachineSide.Bottom => "底面",
            MachineSide.Front => "前面",
            MachineSide.Back => "后面",
            MachineSide.Right => "右面",
            MachineSide.Left => "左面",
            MachineSide.Custom => "自定义",
            _ => ms.ToString()
        };

        public static string ToDisplayTextEn(this MachineSide ms) => ms switch
        {
            MachineSide.Top => "Top",
            MachineSide.Bottom => "Bottom",
            MachineSide.Front => "Front",
            MachineSide.Back => "Back",
            MachineSide.Right => "Right",
            MachineSide.Left => "Left",
            MachineSide.Custom => "Custom",
            _ => ms.ToString()
        };
    }

    /// <summary>GrooveType 扩展方法</summary>
    public static class GrooveTypeExtensions
    {
        public static string ToDisplayText(this GrooveType gt) => gt switch
        {
            GrooveType.General => "通用槽",
            GrooveType.SteelColumn => "钢柱槽",
            GrooveType.XBraceSteel => "斜撑钢槽",
            GrooveType.BaseBracket => "钢柱底板槽",
            GrooveType.TopBracket => "钢柱顶板槽",
            GrooveType.TopPlate => "顶板槽",
            GrooveType.GlueSeal => "胶水密封槽",
            GrooveType.Custom => "自定义",
            _ => gt.ToString()
        };

        public static string ToDisplayTextEn(this GrooveType gt) => gt switch
        {
            GrooveType.General => "General",
            GrooveType.SteelColumn => "Steel Column",
            GrooveType.XBraceSteel => "X-Brace Steel",
            GrooveType.BaseBracket => "Base Bracket",
            GrooveType.TopBracket => "Top Bracket",
            GrooveType.TopPlate => "Top Plate",
            GrooveType.GlueSeal => "Glue Seal",
            GrooveType.Custom => "Custom",
            _ => gt.ToString()
        };
    }

    /// <summary>HoleShape 扩展方法</summary>
    public static class HoleShapeExtensions
    {
        public static string ToDisplayText(this HoleShape hs) => hs switch
        {
            HoleShape.Round => "圆形孔",
            HoleShape.Slotted => "腰孔",
            _ => hs.ToString()
        };

        public static string ToDisplayTextEn(this HoleShape hs) => hs switch
        {
            HoleShape.Round => "Round",
            HoleShape.Slotted => "Slotted",
            _ => hs.ToString()
        };
    }

}
