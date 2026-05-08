namespace CncWallStation.Consts
{
    public static class WallConstants
    {
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
        public const float ColumnSteelGrooveDepth = 100f;

        /// <summary>
        /// 钢柱槽距侧边长度（mm）
        /// </summary>
        public const float ColumnSteelGrooveSideOffset = 50f;

        /// <summary>
        /// 钢柱槽距底边长度（mm）
        /// </summary>
        public const float ColumnSteelGrooveBaseOffset = 1000f;

        // ══════════════════════════════════════════════════════════
        // 钢柱底板槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱底板槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float BaseBracketGrooveDepth = 63f;

        /// <summary>
        /// 钢柱底板槽标准切削长度（mm）
        /// 钢柱嵌入 AAC 墙体的标准长度
        /// </summary>
        public const float BaseBracketGrooveLength = 127f;

        /// <summary>
        /// 钢柱底板槽标准切削宽度（mm）
        /// 钢柱嵌入 AAC 墙体的标准宽度
        /// </summary>
        public const float BaseBracketGrooveWidth = 130f;


        // ══════════════════════════════════════════════════════════
        // 钢柱顶板槽基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 钢柱顶板槽标准切削深度（mm）
        /// 钢柱嵌入 AAC 墙体的标准深度
        /// </summary>
        public const float TopBracketGrooveDepth = 39f;

        /// <summary>
        /// 钢柱顶板槽标准切削长度（mm）
        /// 钢柱嵌入 AAC 墙体的标准长度
        /// </summary>
        public const float TopBracketGrooveLength = 260f;

        /// <summary>
        /// 钢柱顶板槽标准切削宽度（mm）
        /// 钢柱嵌入 AAC 墙体的标准宽度
        /// </summary>
        public const float TopBracketGrooveWidth = 123f;

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
        public const float GlueSealGrooveDepth = 8f;

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
        // BendingKey基础尺寸
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// BendingKey孔半径（mm）
        /// 嵌入 AAC 墙体底面的圆角半径
        /// </summary>
        public const float BendingKeyHoleRadius = 8f;

        /// <summary>
        /// BendingKey孔加工深度（mm）
        /// 嵌入 AAC 墙体底面的标准深度
        /// </summary>
        public const float BendingKeyHoleDepth = 30f;

        /// <summary>
        /// BendingKey 腰孔两圆心距（mm）
        /// </summary>
        public const float BendingKeyHoleSlotLength = 40f;

        /// <summary>
        /// BendingKey 腰孔偏移角度（deg）
        /// </summary>
        public const float BendingKeyHoleSlotAngleDeg = 0f;// 沿 X 轴


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
