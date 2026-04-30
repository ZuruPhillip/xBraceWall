namespace CncWallStation.Features
{
    /// <summary>特征类型</summary>
    public enum FeatureType
    {
        Groove,      // 切槽
        Hole,        // 圆形开孔
        Pocket,      // 矩形挖坑
        MepSlot,     // 电线管道线槽
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

        /// <summary>顶板槽（topPlateGroove）</summary>
        TopPlate,

        /// <summary>斜撑钢槽（xBraceSteelGroove）</summary>
        XBraceSteel,

        /// <summary>底板槽</summary>
        BottomPlate,

        /// <summary>侧板槽</summary>
        SidePanel,

        /// <summary>可扩展自定义类型</summary>
        Custom
    }
}
