using CncWallStation.Features;
using CncWallStation.Features.MepSlots;
using Infrastructure.Maths;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// 线槽处理器（T8 F5/F6/F7/F8）
    ///
    ///   方向判断规则：
    ///     水平/垂直             → F6
    ///     左上→右下 (↘)          → F5
    ///     左下→右上 (↗)          → F6
    ///     右下→左上 (↖)          → F7
    ///     右上→左下 (↙)          → F8
    ///
    ///   字段映射：
    ///     T  = 8 (SlotCutter)
    ///     X0/Y0 = 起点坐标
    ///     X1/Y1 = 水平/垂直偏移量
    ///     Z0 = 0
    ///     Z1 = 线槽切割深度
    /// </summary>
    public static class CableHandler
    {
        private const float Epsilon = 1e-3f;

        /// <summary>
        /// 批量处理 MepSlot 列表：遍历每段直线，按方向发射指令
        /// </summary>
        public static void HandleBatch(List<MepSlot> slots, PlcConvertContext ctx)
        {
            foreach (var slot in slots)
            {
                foreach (var seg in slot.Segments)
                {
                    if (seg is not LineSegment line)
                        continue;

                    float dx = line.EndPoint.X - line.StartPoint.X;
                    float dy = line.EndPoint.Y - line.StartPoint.Y;

                    int f = DetermineF(dx, dy);

                    ctx.Emit(new PlcInstruction
                    {
                        T = PlcTool.SlotCutter,  // T8
                        F = f,
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
        }

        // ══════════════════════════════════════════════════════
        // 方向 → F 值映射
        // ══════════════════════════════════════════════════════

        private static int DetermineF(float dx, float dy)
        {
            bool horizontal = MathF.Abs(dy) < Epsilon;
            bool vertical   = MathF.Abs(dx) < Epsilon;

            if (horizontal || vertical)
                return PlcFeatureCode.CableSlotL6; // F6

            // 对角方向（按起点→终点的象限判断）
            if (dx > 0 && dy > 0)
                return PlcFeatureCode.CableSlotL5; // F5  左上→右下
            if (dx > 0 && dy < 0)
                return PlcFeatureCode.CableSlotL6; // F6  左下→右上
            if (dx < 0 && dy > 0)
                return PlcFeatureCode.CableSlotL7; // F7  右下→左上
            if (dx < 0 && dy < 0)
                return PlcFeatureCode.CableSlotL8; // F8  右上→左下

            return PlcFeatureCode.CableSlotL6; // fallback
        }
    }
}
