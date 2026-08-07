using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
using Infrastructure.Maths;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// 线槽处理器（T8）
    ///
    ///   分区类型与指令映射：
    ///     waffleBox (OverrideWidth != 0) → T8 F10 (CableBox)
    ///     arc      (含圆弧段)           → T8 F6 (顺时针) / F7 (逆时针)
    ///     line     (纯直线段)           → T8 F6
    ///
    ///   waffleBox 字段映射：
    ///     X0 = 分段起点 X
    ///     Y0 = 分段起点 Y - OverrideWidth / 2
    ///     X1 = OverrideWidth
    ///     Y1 = 所有直线段总长度
    ///
    ///   arc 字段映射：
    ///     X0/Y0 = 分区起点（含前半段直线起点）
    ///     X1/Y1 = 分区终点 - 分区起点（dx/dy）
    ///
    ///   入参为 MepSlotPartitioner 生成的分段集合。
    /// </summary>
    public static class CableHandler
    {
        private const float Epsilon = 1e-3f;

        /// <summary>
        /// 批量处理 MepSlot 分段集合：按分区类型发射对应指令。
        /// </summary>
        public static void HandleBatch(List<MepSlot> slots, PlcConvertContext ctx)
        {
            foreach (var slot in slots)
            {
                var kind = Classify(slot);

                switch (kind)
                {
                    case PartitionKind.WaffleBox:
                        EmitWaffleBox(slot, ctx);
                        break;
                    case PartitionKind.Arc:
                        EmitArc(slot, ctx);
                        break;
                    case PartitionKind.Line:
                        EmitLine(slot, ctx);
                        break;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 分区分类
        // ══════════════════════════════════════════════════════

        private enum PartitionKind { WaffleBox, Arc, Line }

        /// <summary>判断分区类型</summary>
        private static PartitionKind Classify(MepSlot slot)
        {
            if (slot.Segments.Count == 0)
                return PartitionKind.Line;

            // waffleBox：任一段的 OverrideWidth 不为 null 且不为 0
            foreach (var seg in slot.Segments)
            {
                if (seg.OverrideWidth.HasValue && seg.OverrideWidth.Value != 0f)
                    return PartitionKind.WaffleBox;
            }

            // arc：包含 ArcSegment
            foreach (var seg in slot.Segments)
            {
                if (seg is ArcSegment)
                    return PartitionKind.Arc;
            }

            return PartitionKind.Line;
        }

        // ══════════════════════════════════════════════════════
        // 指令发射
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// waffleBox 指令：T8 F10
        ///   X0 = 分段起点 X
        ///   Y0 = 分段起点 Y - OverrideWidth / 2
        ///   X1 = OverrideWidth
        ///   Y1 = 所有直线段总长度
        /// </summary>
        private static void EmitWaffleBox(MepSlot slot, PlcConvertContext ctx)
        {
            Vec2 start = slot.Segments[0].StartPoint;
            float overrideW = GetOverrideWidth(slot);

            // 计算所有直线段总长度
            float totalLineLength = 0f;
            foreach (var seg in slot.Segments)
            {
                if (seg is LineSegment line)
                    totalLineLength += line.Length;
            }

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter,       // T8
                F = PlcFeatureCode.CableBox,  // F10
                D = 0,
                X0 = start.X - overrideW * 0.5f,
                Y0 = start.Y,
                Z0 = 0f,
                X1 = overrideW,
                Y1 = totalLineLength,
                Z1 = slot.Segments[0].Depth
            });
        }

        /// <summary>
        /// arc 指令：T8 F6 (顺时针) / F7 (逆时针)
        ///   X0/Y0 = 分区起点
        ///   X1/Y1 = 分区终点 - 分区起点
        /// </summary>
        private static void EmitArc(MepSlot slot, PlcConvertContext ctx)
        {
            Vec2 start = slot.Segments[0].StartPoint;
            Vec2 end = slot.Segments[^1].EndPoint;

            // 找到 ArcSegment 判断方向
            bool isClockwise = true; // 默认
            foreach (var seg in slot.Segments)
            {
                if (seg is ArcSegment arc)
                {
                    isClockwise = arc.IsClockwise;
                    break;
                }
            }

            int f = isClockwise
                ? PlcFeatureCode.CableSlotL6  // F6 顺时针
                : PlcFeatureCode.CableSlotL7; // F7 逆时针

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter, // T8
                F = f,
                D = 0,
                X0 = start.X,
                Y0 = start.Y,
                Z0 = 0f,
                X1 = end.X - start.X,
                Y1 = end.Y - start.Y,
                Z1 = slot.Segments[0].Depth
            });
        }

        /// <summary>
        /// line 指令：T8 F6
        ///   遍历每条直线段发射指令
        /// </summary>
        private static void EmitLine(MepSlot slot, PlcConvertContext ctx)
        {
            foreach (var seg in slot.Segments)
            {
                if (seg is not LineSegment line)
                    continue;

                float dx = line.EndPoint.X - line.StartPoint.X;
                float dy = line.EndPoint.Y - line.StartPoint.Y;

                ctx.Emit(new PlcInstruction
                {
                    T = PlcTool.SlotCutter,       // T8
                    F = PlcFeatureCode.CableSlotL6, // F6
                    D = 0,
                    X0 = line.StartPoint.X,
                    Y0 = line.StartPoint.Y,
                    Z0 = 0f,
                    X1 = dx,
                    Y1 = dy,
                    Z1 = line.Depth
                });
            }
        }

        // ══════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════

        /// <summary>从分区中提取 OverrideWidth 值（取第一个非 null 非零的值）</summary>
        private static float GetOverrideWidth(MepSlot slot)
        {
            foreach (var seg in slot.Segments)
            {
                if (seg.OverrideWidth.HasValue && seg.OverrideWidth.Value != 0f)
                    return seg.OverrideWidth.Value;
            }
            return 0f;
        }
    }
}
