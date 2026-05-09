using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;
using static CncWallStation.Plcs.GrooveSideClassifier;

namespace CncWallStation.Plcs.Handlers
{
    public static class StepHandler
    {
        public static void Handle(Groove g, PlcConvertContext ctx, MomWall wall)
        {
            if (g.GrooveType is not GrooveType.SteelColumn) return;

            // ── 自动判定方向 ──
            GrooveSide side = GrooveSideClassifier.Classify(g, wall);
            if (side == GrooveSide.None)
            {
                // 槽不贴边 — 钢筋槽必须沿墙边
                return;
            }

            // 映射到 F 值
            int f = side.ToAutoStepFCode();

            // ── 装填指令 ──
            var (p0, p1, p2, p3) = g.GetCorners();
            float minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
            float maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
            float minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
            float maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

            //    Bottom → (minX, minY)   左下
            //    Left   → (minX, maxY)   左上
            //    Top    → (maxX, maxY)   右上
            //    Right  → (maxX, minY)   右下
            (float x0, float y0) = side switch
            {
                GrooveSide.Bottom => (minX, minY),
                GrooveSide.Left => (minX, maxY),
                GrooveSide.Top => (maxX, maxY),
                GrooveSide.Right => (maxX, minY),
                _ => throw new InvalidOperationException($"Unsupported side: {side}")
            };

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.StepCutter,    // T4
                F = f,
                D = 0,
                X0 = minX,
                Y0 = minY,
                Z0 = 0,
                X1 = maxX - minX,           // 槽长（X 方向跨度）
                Y1 = maxY - minY,           // 槽宽（Y 方向跨度）
                Z1 = g.Depth
            });
        }


        /// <summary>
        /// GrooveSide → T4 自动补偿 F 值
        ///
        ///   Bottom → F5（Y -50 补偿）
        ///   Left   → F6（X -50 补偿）
        ///   Top    → F7（Y +50 补偿）
        ///   Right  → F8（X +50 补偿）
        /// </summary>
        public static int ToAutoStepFCode(this GrooveSide side) => side switch
        {
            GrooveSide.Bottom => PlcFeatureCode.StepBtm_Auto,    // F5
            GrooveSide.Left => PlcFeatureCode.StepLeft_Auto,     // F6
            GrooveSide.Top => PlcFeatureCode.StepTop_Auto,       // F7
            GrooveSide.Right => PlcFeatureCode.StepRight_Auto,   // F8
            GrooveSide.None => throw new InvalidOperationException(
                                    "Groove 未贴墙边，无法映射 T4 F 值"),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
        };

    }
}
