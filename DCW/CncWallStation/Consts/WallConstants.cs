namespace CncWallStation.Consts
{
    public static class WallConstants
    {
        #region Tolerance
        public const float DirectionTolerance = 1e-3f;            // 方向判定容差（mm）
        #endregion

        #region 墙体产品参数
        // ══════════════════════════════════════════════════════════
        // 钢柱槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱槽标准总宽度（mm）
        /// </summary>
        public const float ColumnSteelGrooveWidth = 100f;

        /// <summary>
        /// 钢柱槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float ColumnSteelGrooveDepth = 103f;

        /// <summary>
        /// 钢柱槽距侧边长度（mm）
        /// </summary>
        public const float ColumnSteelGrooveSideOffset = 50f;

        /// <summary>
        /// 钢柱槽距底边长度（mm）
        /// 钢柱槽下段（墙底 → 该高度）与上段（该高度 → 墙顶）的分界高度
        /// </summary>
        public const float ColumnSteelGrooveBaseOffset = 1000f;

        /// <summary>
        /// 钢柱槽下段（墙底 → 1000mm）标准总宽度（mm）
        /// 槽外侧贴齐墙体侧边，宽度自侧边向内计
        /// </summary>
        public const float ColumnSteelGrooveLowerWidth = 103f;

        /// <summary>
        /// 钢柱槽上段（1000mm → 墙顶）标准总宽度（mm）
        /// 槽外侧贴齐墙体侧边，宽度自侧边向内计
        /// </summary>
        public const float ColumnSteelGrooveUpperWidth = 178f;

        // ══════════════════════════════════════════════════════════
        // 钢柱底板槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱底板槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float BaseBracketGrooveDepth = 103f;

        /// <summary>
        /// 钢柱底板槽标准切削长度（mm）
        /// 钢柱嵌入 AAC 墙体的标准长度
        /// </summary>
        public const float BaseBracketGrooveLength = 228f;

        /// <summary>
        /// 钢柱底板槽标准切削宽度（mm）
        /// 钢柱嵌入 AAC 墙体的标准宽度
        /// </summary>
        public const float BaseBracketGrooveWidth = 37f;


        // ══════════════════════════════════════════════════════════
        // 钢柱顶板槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱顶板槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float TopBracketGrooveDepth = 123f;

        /// <summary>
        /// 钢柱顶板槽标准切削长度（mm）
        /// 钢柱嵌入 AAC 墙体的标准长度
        /// </summary>
        public const float TopBracketGrooveLength = 260f;

        /// <summary>
        /// 钢柱顶板槽标准切削宽度（mm）
        /// 钢柱嵌入 AAC 墙体的标准宽度
        /// </summary>
        public const float TopBracketGrooveWidth = 39f;

        // ══════════════════════════════════════════════════════════
        // 顶板槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱顶板槽标准切削宽度（mm）
        /// 钢柱嵌入 AAC 墙体的标准宽度
        /// </summary>
        public const float TopPlateGrooveWidth = 240f;

        /// <summary>
        /// 钢柱顶板槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float TopPlateGrooveDepth = 9f;

        /// <summary>
        /// 剪力钉孔切削深度（mm）
        /// </summary>
        public const float StudHoleDepth = 42f;

        /// <summary>剪力钉腰形孔半径（mm）——两端半圆半径（= StudDiameter / 2）</summary>
        public const float StudSlotHoleRadius = 12.5f;

        /// <summary>剪力钉腰形孔总长度（mm，含两端半圆）</summary>
        public const float StudSlotHoleTotalLength = 225f;

        /// <summary>剪力钉腰形孔切削深度（mm）</summary>
        public const float StudSlotHoleDepth = 114f;

        // ══════════════════════════════════════════════════════════
        // 顶板槽底部 Pocket 尺寸（贴顶板槽槽底，Y = wallWidth - TopPlateGrooveDepth - 宽度/2）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 顶板槽底部 Pocket 0（最外层）长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float TopPlatePocket0Length = 106f;

        /// <summary>
        /// 顶板槽底部 Pocket 0（最外层）宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float TopPlatePocket0Width = 310f;

        /// <summary>
        /// 顶板槽底部 Pocket 0（最外层）深度（mm）
        /// </summary>
        public const float TopPlatePocket0Depth = 4f;

        /// <summary>
        /// 顶板槽底部 Pocket 1 长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float TopPlatePocket1Length = 106f;

        /// <summary>
        /// 顶板槽底部 Pocket 1 宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float TopPlatePocket1Width = 18f;

        /// <summary>
        /// 顶板槽底部 Pocket 1 深度（mm）
        /// </summary>
        public const float TopPlatePocket1Depth = 36f;

        /// <summary>
        /// 顶板槽底部 Pocket 2 长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float TopPlatePocket2Length = 252f;

        /// <summary>
        /// 顶板槽底部 Pocket 2 宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float TopPlatePocket2Width = 4f;

        /// <summary>
        /// 顶板槽底部 Pocket 2 深度（mm）
        /// </summary>
        public const float TopPlatePocket2Depth = 100f;

        // ══════════════════════════════════════════════════════════
        // 胶水密封槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 胶水密封槽槽标准切削宽度（mm）
        /// </summary>
        public const float GlueSealGrooveWidth = 36f;

        /// <summary>
        /// 胶水密封槽标准切削深度（mm）
        /// </summary>
        public const float GlueSealGrooveDepth = 15f;

        // ══════════════════════════════════════════════════════════
        // Mep Cable基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// MepCable Slot 槽宽（mm）
        /// </summary>
        public const float MepCableSlotWidth = 30f;

        /// <summary>
        /// MepCable Slot 槽深（mm）
        /// </summary>
        public const float MepCableSlotDepth = 18f;

        /// <summary>
        /// MepCable Slot 圆弧倒角（mm）
        /// </summary>
        public const float MepCableSlotCornerRadius = 60f;

        /// <summary>
        /// 华夫盒线槽切削宽度（mm）
        /// </summary>
        public const float WaffleBoxWidth = 100f;

        /// <summary>
        /// 华夫盒线槽切削长度（mm）
        /// </summary>

        public const float WaffleBoxLength = 100f;

        /// <summary>
        /// 开关盒近端线槽切削深度（mm）
        /// </summary>

        public const float DeviceDepth = 60f;

        /// <summary>
        /// 开关盒近端线槽切削长度（mm）
        /// </summary>

        public const float DeviceTaperLen = 60f;

        // ══════════════════════════════════════════════════════════
        // Mep Device基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 开关盒切削宽度（mm）
        /// </summary>
        public const float DevicePocketWidth = 120f;

        /// <summary>
        /// 开关盒切削长度（mm）
        /// </summary>
        public const float DevicePocketLength = 160f;

        /// <summary>
        /// 开关盒切削深度（mm）
        /// </summary>
        public const float DevicePocketDepth = 60f;

        /// <summary>
        /// 开关盒切削圆角半径（mm）
        /// </summary>
        public const float DevicePocketCornerRadius = 10f;


        // ══════════════════════════════════════════════════════════
        // BendingKey基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// BendingKey孔半径（mm）
        /// 嵌入 AAC 墙体底面的圆角半径
        /// </summary>
        public const float BendingKeyHoleRadius = 12.5f;

        /// <summary>
        /// BendingKey孔加工深度（mm）
        /// 嵌入 AAC 墙体底面的标准深度
        /// </summary>
        public const float BendingKeyHoleDepth = 105f;

        /// <summary>
        /// BendingKey 腰孔两圆心距（mm）
        /// </summary>
        public const float BendingKeyHoleSlotLength = 80f;

        /// <summary>
        /// BendingKey 腰孔偏移角度（deg）
        /// </summary>
        public const float BendingKeyHoleSlotAngleDeg = 0f;// 沿 X 轴

        // ══════════════════════════════════════════════════════════
        // XBrace槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// XBrace槽偏移墙上边距离（mm）
        /// </summary>
        public const float XBraceTopOffset = 325f;   // 向外（上方）偏移

        /// <summary>
        /// XBrace槽偏移墙下边距离（mm）
        /// </summary>
        public const float XBraceBottomOffset = 325f;   // 向外（下方）偏移

        /// <summary>
        /// XBrace槽下侧（朝墙底方向）槽宽（mm）
        /// </summary>
        public const float XBraceGrooveWidth = 80f;    // 下侧槽宽 (mm)

        /// <summary>
        /// XBrace槽上侧（朝墙顶方向）槽宽（mm）
        /// </summary>
        public const float XBraceUpperGrooveWidth = 106f;   // 上侧槽宽 (mm)

        /// <summary>
        /// XBrace槽深度（mm）
        /// </summary>
        public const float XBraceGrooveDepth = 90f;    // 槽深 (mm)

        /// <summary>
        /// XBrace槽自交点沿交线方向的起槽偏移（mm）
        /// </summary>
        public const float XBraceCenterOffset = 450f;

        /// <summary>
        /// XBrace交点处槽沿交线方向长度（mm）
        /// </summary>
        public const float XBraceCenterGrooveLength = 100f;

        /// <summary>
        /// XBrace交点处槽宽度（mm）
        /// </summary>
        public const float XBraceCenterGrooveWidth = 150f;

        /// <summary>
        /// XBrace交点处槽深度（mm）
        /// </summary>
        public const float XBraceCenterGrooveDepth = 120f;

        /// <summary>
        /// XBrace交点处槽中心点距交点距离（mm）
        /// </summary>
        public const float XBraceCenterGrooveOffset = 500f;

        // ══════════════════════════════════════════════════════════
        // 钢柱侧板槽基础尺寸（Pocket 方式加工）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱侧板槽长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float SidePlateGrooveLength = 180f;

        /// <summary>
        /// 钢柱侧板槽外槽宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float SidePlateGrooveOuterWidth = 200f;

        /// <summary>
        /// 钢柱侧板槽外槽深度（mm）
        /// </summary>
        public const float SidePlateGrooveOuterDepth = 15f;

        /// <summary>
        /// 钢柱侧板槽内槽宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float SidePlateGrooveInnerWidth = 120f;

        /// <summary>
        /// 钢柱侧板槽内槽深度（mm）
        /// </summary>
        public const float SidePlateGrooveInnerDepth = 40f;

        /// <summary>
        /// 钢柱侧板槽中心点定位：判定柱子位于左侧的 X 阈值（mm）
        /// 柱 X 小于该值视为左侧柱，否则按右侧柱处理
        /// </summary>
        public const float SidePlateColumnLeftThreshold = 200f;

        /// <summary>
        /// 钢柱侧板槽下中心点 Y 偏移（mm）：自 ColumnSteelGrooveBaseOffset 向上
        /// </summary>
        public const float SidePlateGrooveLowerOffset = 294f;

        /// <summary>
        /// 钢柱侧板槽上中心点 Y 偏移（mm）：自墙顶（Wall.Width）向下
        /// </summary>
        public const float SidePlateGrooveUpperOffset = 604f;

        // ── 钢柱侧板小槽（Pocket 方式加工）──────────────────────

        /// <summary>
        /// 钢柱侧板小槽长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float SidePlateSmallGrooveLength = 160f;

        /// <summary>
        /// 钢柱侧板小槽宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float SidePlateSmallGrooveWidth = 120f;

        /// <summary>
        /// 钢柱侧板小槽深度（mm）
        /// </summary>
        public const float SidePlateSmallGrooveDepth = 40f;

        /// <summary>
        /// 钢柱侧板小槽窄槽长度（mm）——沿墙长方向（X）
        /// </summary>
        public const float SidePlateNarrowGrooveLength = 15f;

        /// <summary>
        /// 钢柱侧板小槽窄槽宽度（mm）——沿墙高方向（Y）
        /// </summary>
        public const float SidePlateNarrowGrooveWidth = 120f;

        /// <summary>
        /// 钢柱侧板小槽窄槽深度（mm）
        /// </summary>
        public const float SidePlateNarrowGrooveDepth = 55f;

        /// <summary>
        /// 钢柱侧板小槽中心点 Y（mm）——自墙底向上
        /// </summary>
        public const float SidePlateSmallGrooveCenterY = 285f;

        /// <summary>
        /// 钢柱侧板螺栓孔组：孔位相对中心点的 X 偏移（mm）
        /// </summary>
        public const float SidePlateBoltHoleOffsetX = 60f;

        /// <summary>
        /// 钢柱侧板螺栓孔组：孔位相对中心点的 Y 偏移（mm）
        /// </summary>
        public const float SidePlateBoltHoleOffsetY = 80f;

        /// <summary>
        /// 钢柱侧板螺栓孔组（4 孔方阵）：孔位相对中心点的 X / Y 偏移（mm）
        /// </summary>
        public const float SidePlateBoltHoleSquareOffset = 30f;

        /// <summary>
        /// 钢柱侧板螺栓孔直径（mm）
        /// </summary>
        public const float SidePlateScrewHoleDiameter = 20f;

        /// <summary>
        /// 钢柱侧板螺栓孔孔深（mm）
        /// </summary>
        public const float SidePlateScrewHoleDepth = 100f;

        // ══════════════════════════════════════════════════════════
        // 斜撑槽基础尺寸
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 斜撑槽顶面总长度（mm）
        /// </summary>
        public const float ProppingTopSlotLength = 300f;// 沿 Y 轴

        /// <summary>
        /// 斜撑槽顶面总宽度（mm）
        /// </summary>
        public const float ProppingTopSlotWidth = 100f;// 沿 X 轴

        /// <summary>
        /// 斜撑槽顶面中间长度（mm）
        /// </summary>
        public const float ProppingTopSlotMidLength = 200f;// 沿 Y 轴

        /// <summary>
        /// 斜撑槽顶面中间宽度（mm）
        /// </summary>
        public const float ProppingTopSlotMidWidth = 60f;// 沿 X 轴

        /// <summary>
        /// 斜撑槽顶面深度（mm）
        /// </summary>
        public const float ProppingTopSlotDepth = 30f;// 沿 X 轴

        /// <summary>
        /// 斜撑槽侧面总长度（mm）
        /// </summary>
        public const float ProppingSideSlotLength = 100f;// 沿 Z 轴

        /// <summary>
        /// 斜撑槽侧面总宽度（mm）
        /// </summary>
        public const float ProppingSideSlotWidth = 300f;// 沿 X 轴

        /// <summary>
        /// 斜撑槽侧面总长度（mm）
        /// </summary>
        public const float ProppingSideSlotEdgeLength = 30f;// 沿 Z 轴

        /// <summary>
        /// 斜撑槽侧面总宽度（mm）
        /// </summary>
        public const float ProppingSideSlotEdgeWidth = 100f;// 沿 X 轴

        /// <summary>
        /// 斜撑槽侧面深度（mm）
        /// </summary>
        public const float ProppingSideSlotDepth = 30f;// 沿 Y 轴

        /// <summary>
        /// 斜撑顶部螺栓孔 X 向偏移（mm）——孔位 X = 中心 X ± 该值
        /// </summary>
        public const float ProppingTopBoltHoleOffsetX = 30f;

        /// <summary>
        /// 斜撑顶部螺栓孔距墙顶距离（mm）——孔位 Y = wallWidth - 该值
        /// </summary>
        public const float ProppingTopBoltHoleOffsetY = 299f;

        /// <summary>
        /// 斜撑正面螺栓孔 X 向偏移（mm）——孔位 X = 中心 X ± 该值
        /// </summary>
        public const float ProppingFrontBoltHoleOffsetX = 70f;

        /// <summary>
        /// 斜撑正面螺栓孔距墙顶距离（mm）——孔位 Z = wallHeight - 该值
        /// </summary>
        public const float ProppingFrontBoltHoleOffsetY = 75f;

        /// <summary>
        /// 斜撑螺栓孔直径（mm）
        /// </summary>
        public const float ProppingScrewHoleDiameter = 20f;

        /// <summary>
        /// 斜撑螺栓孔孔深（mm）
        /// </summary>
        public const float ProppingScrewHoleDepth = 100f;

        /// <summary>横向钢筋槽切割深度（mm）</summary>
        public const float HorizontalRebarSlotDepth = 28f;

        /// <summary>纵向钢筋槽切割深度（mm）</summary>
        public const float VerticalRebarSlotDepth = 35f;

        /// <summary>钢筋槽宽度（mm）</summary>
        public const float RebarSlotWidth = 8f;

        /// <summary>Studs距边缘距离（mm）</summary>
        public const float StudEdgeDistance = 107f;

        /// <summary>Studs直径（mm）</summary>
        public const float StudDiameter = 25f;
        #endregion


        #region PLC 刀具参数

        public const float SlotToolDiameter = 20f;

        #endregion
        ///// <summary>
        ///// 槽最小切削深度（mm）
        ///// 低于此值视为加工不足，需告警
        ///// </summary>
        //public const float GrooveDepthMin = 0f;

        ///// <summary>
        ///// 槽最大切削深度（mm）
        ///// 超过此值视为过切，需告警
        ///// </summary>
        //public const float GrooveDepthMax = 40f;

        // ══════════════════════════════════════════════════════════
        // 加工余量（Clearance）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 单侧宽度加工余量（mm）
        /// 槽宽在钢柱每侧的额外加工间隙，用于安装配合
        /// </summary>
        public const float ColumnSteelWidthClearancePerSide = 3f;

        /// <summary>
        /// 双侧宽度加工余量合计（mm）
        /// = WidthClearancePerSide × 2
        /// </summary>
        public const float ColumnSteelWidthClearanceTotal = ColumnSteelWidthClearancePerSide * 2f;

        /// <summary>
        /// 深度方向加工余量（mm）
        /// 槽底部预留的额外加工深度，防止干涉
        /// </summary>
        public const float ColumnSteelDepthClearance = 3f;
    }
}
