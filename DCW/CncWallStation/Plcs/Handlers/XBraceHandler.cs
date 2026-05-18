using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;
using Infrastructure.Maths;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// X 形斜撑钢槽处理器（T8 F30 / F31）
    ///
    ///   ┌─────────────────────┬─────┬────────────────────────────┐
    ///   │ 槽方向                │ F  │ 字段映射                     │
    ///   ├─────────────────────┼─────┼────────────────────────────┤
    ///   │ 左下 → 右上（↗）      │ 30 │ (X0,Y0)=起点低, (X1,Y1)=终点高│
    ///   │ 左上 → 右下（↘）      │ 31 │ (X0,Y0)=起点高, (X1,Y1)=终点低│
    ///   └─────────────────────┴─────┴────────────────────────────┘
    ///
    /// 字段映射：
    ///   T  = 8           （Cable 刀，与斜槽共用）
    ///   F  = 30 / 31     （由斜率方向决定）
    ///   D  = 槽宽度      （注意：此处 D ≠ 复制次数，而是物理宽度 mm）
    ///   X0 = 起点 X
    ///   Y0 = 起点 Y
    ///   Z0 = 0
    ///   X1 = 终点 X
    ///   Y1 = 终点 Y
    ///   Z1 = 槽深
    ///
    /// 输入约束：
    ///   • Groove.GrooveType == GrooveType.XBraceSteel
    ///   • Groove.StartPt / EndPt 必须斜向（dx≠0 且 dy≠0）
    ///   • 加工面：MachineSide.Top（墙正面）
    /// </summary>
    public static class XBraceHandler
    {
        private const float Tol = 1e-3f;

        public static void Handle(Groove g, PlcConvertContext ctx, MomWall wall)
        {
            // 1. 类型校验
            if (g.GrooveType != GrooveType.XBraceSteel)
                throw new InvalidOperationException(
                    $"[XBrace {g.Id}] 槽类型应为 XBraceSteel，实际: {g.GrooveType}");

            // 2. 加工面校验（X 斜槽仅墙正面）
            if (g.Face.InitialSide != MachineSide.Top)
                throw new NotSupportedException(
                    $"[XBrace {g.Id}] 仅支持 Top 面加工，实际: {g.Face.InitialSide}");

            // 3. 标准化端点：保证 X0 < X1（始终从左向右扫描）
            var (start, end) = NormalizeLeftToRight(g.StartPt, g.EndPt);

            // 4. 严格斜向校验
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;

            if (MathF.Abs(dx) <= Tol || MathF.Abs(dy) <= Tol)
                throw new InvalidOperationException(
                    $"[XBrace {g.Id}] 必须为斜向 (dx={dx:F3}, dy={dy:F3})");


            bool wide = g.Width > WallConstants.SlotToolDiameter * 2;

            int f = (dy > 0, wide) switch
            {
                (true, false) => PlcFeatureCode.XBraceUpRight,       // 30
                (false, false) => PlcFeatureCode.XBraceDownRight,     // 31
                (true, true) => PlcFeatureCode.XBraceUpRightWide,   // 32
                (false, true) => PlcFeatureCode.XBraceDownRightWide, // 33
            };

            // 6. 发射指令
            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter,         // T8
                F = f,
                D = (int)g.Width,          // D = 槽宽度（注意：不是复制次数）
                X0 = start.X,
                Y0 = start.Y,
                Z0 = 0f,
                X1 = end.X,
                Y1 = end.Y,
                Z1 = g.Depth
            });
        }

        // ══════════════════════════════════════════════════════
        // 标准化：始终保证 X0 ≤ X1，方便由 dy 符号判定方向
        // ══════════════════════════════════════════════════════

        private static (Vec2 start, Vec2 end)
            NormalizeLeftToRight(Vec2 a, Vec2 b)
        {
            return a.X <= b.X ? (a, b) : (b, a);
        }
    }
}