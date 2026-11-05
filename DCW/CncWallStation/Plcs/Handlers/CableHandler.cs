using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
using Infrastructure.Maths;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// 线槽处理器（T8）
    ///
    ///   分区类型与指令映射：
    ///     waffleBox (OverrideWidth != 0) → T8 F10 (CableBox)
    ///     arc      (含圆弧段)           → T8 F5 / F6 / F7 / F8（按分区起→终方位）
    ///     line     (纯直线段)           → T8 F6
    ///
    ///   arc 方位映射（PLC 侧圆弧指令恒按逆时针解释；dx/dy = 分区终点 - 分区起点，
    ///   墙局部坐标 Y 增大 = 上）：
    ///     dx &gt; 0, dy &lt; 0 → F5（左上→右下）
    ///     dx &gt; 0, dy &gt; 0 → F6（左下→右上）
    ///     dx &lt; 0, dy &gt; 0 → F7（右下→左上）
    ///     dx &lt; 0, dy &lt; 0 → F8（右上→左下）
    ///     |dx| 或 |dy| ≈ 0 → 方位退化，回退 F6 并记日志
    ///
    ///   waffleBox 字段映射：
    ///     X0 = 分段起点 X - OverrideWidth / 2
    ///     Y0 = 分段起点 Y
    ///     X1 = OverrideWidth
    ///     Y1 = 所有直线段总长度
    ///
    ///   arc 字段映射：
    ///     X0/Y0 = 分区起点（含前半段直线起点）
    ///     X1/Y1 = 分区终点 - 分区起点（dx/dy）
    ///
    ///   入参为 MepSlotPartitioner 生成的分段集合（圆弧已归一化为逆时针）。
    /// </summary>
    public static class CableHandler
    {
        private const float Epsilon = 1e-3f;

        /// <summary>
        /// 批量处理 MepSlot 分段集合：按分区类型发射对应指令。
        /// </summary>
        /// <param name="slots">MepSlotPartitioner 生成的圆弧已归一化为逆时针的分段集合</param>
        /// <param name="ctx">PLC 转换上下文</param>
        /// <param name="logger">可选日志；用于圆弧方位退化告警</param>
        public static void HandleBatch(
            List<MepSlot> slots, PlcConvertContext ctx, ILogger? logger = null)
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
                        EmitArc(slot, ctx, logger);
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
        /// arc 指令：T8 F5 / F6 / F7 / F8（按分区起点→终点方位）
        ///   X0/Y0 = 分区起点
        ///   X1/Y1 = 分区终点 - 分区起点（dx/dy）
        ///   圆弧恒按逆时针解释，不再读取 ArcSegment.IsClockwise。
        /// </summary>
        private static void EmitArc(MepSlot slot, PlcConvertContext ctx, ILogger? logger)
        {
            Vec2 start = slot.Segments[0].StartPoint;
            Vec2 end = slot.Segments[^1].EndPoint;

            float dx = end.X - start.X;
            float dy = end.Y - start.Y;

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter, // T8
                F = ResolveArcFeatureCode(dx, dy, slot, logger),
                D = 0,
                X0 = start.X,
                Y0 = start.Y,
                Z0 = 0f,
                X1 = dx,
                Y1 = dy,
                Z1 = slot.Segments[0].Depth
            });
        }

        /// <summary>
        /// 按分区起点→终点的方位解析圆弧指令 F 值（圆弧恒逆时针）。
        /// dx/dy 任一接近 0 视为方位退化，回退 F6 并记日志。
        /// </summary>
        private static int ResolveArcFeatureCode(
            float dx, float dy, MepSlot slot, ILogger? logger)
        {
            if (MathF.Abs(dx) <= Epsilon || MathF.Abs(dy) <= Epsilon)
            {
                logger?.LogWarning(
                    "[CableHandler] 圆弧分区 {MepSlotId} 方位增量退化(dx={Dx:F3}, dy={Dy:F3})，" +
                    "无法判定四方位，回退 T8F6",
                    slot.Id, dx, dy);
                return PlcFeatureCode.CableSlotL6;
            }

            if (dx > 0f)
                // dx>0：左 → 右；dy<0 为左上→右下(F5)，dy>0 为左下→右上(F6)
                return dy < 0f ? PlcFeatureCode.CableSlotL5 : PlcFeatureCode.CableSlotL6;

            // dx<0：右 → 左；dy>0 为右下→左上(F7)，dy<0 为右上→左下(F8)
            return dy > 0f ? PlcFeatureCode.CableSlotL7 : PlcFeatureCode.CableSlotL8;
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
