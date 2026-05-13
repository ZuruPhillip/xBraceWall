using CncWallStation.Features;
using CncWallStation.Features.Props;
using Infrastructure.Maths;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// Propping 处理器（仅 Top / Bottom 面）
    ///
    ///   多边形 → 水平分割 → 四边形 → BoundingBox 加工
    ///   指令格式与 BoxHandler 一致：
    ///     T  = 8 (SlotCutter)
    ///     F  = 10 (CableBox)
    ///     X0 = minX, Y0 = minY
    ///     X1 = width, Y1 = height
    ///     Z1 = Depth
    /// </summary>
    public static class ProppingHandler
    {
        private const float Epsilon = 1e-6f;

        /// <summary>
        /// 处理 Propping：仅转换 Top / Bottom 面的 Cuts
        /// </summary>
        public static void Handle(Propping p, PlcConvertContext ctx)
        {
            foreach (var cut in p.Cuts)
            {
                if (cut.Side != MachineSide.Top && cut.Side != MachineSide.Bottom)
                    continue;

                var outline = cut.Outline;
                if (outline.Count == 0) continue;

                if (outline.Count <= 4)
                {
                    // 简单多边形：直接用包围盒
                    EmitBBox(outline, cut.Depth, ctx);
                }
                else
                {
                    // 复杂多边形：水平分割为多个四边形带
                    foreach (var strip in SplitHorizontal(outline))
                    {
                        EmitBBox(strip, cut.Depth, ctx);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 指令发射（与 BoxHandler 一致）
        // ══════════════════════════════════════════════════════

        private static void EmitBBox(List<Vec2> outline, float depth, PlcConvertContext ctx)
        {
            var (minX, minY, maxX, maxY) = ComputeBBox(outline);

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter,
                F = PlcFeatureCode.CableBox,
                D = 0,
                X0 = minX,
                Y0 = minY,
                Z0 = 0f,
                X1 = maxX - minX,
                Y1 = maxY - minY,
                Z1 = depth
            });
        }

        private static (float minX, float minY, float maxX, float maxY) ComputeBBox(
            List<Vec2> pts)
        {
            float minX = pts[0].X, minY = pts[0].Y;
            float maxX = minX, maxY = minY;
            for (int i = 1; i < pts.Count; i++)
            {
                var p = pts[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            return (minX, minY, maxX, maxY);
        }

        // ══════════════════════════════════════════════════════
        // 水平分割：按 Y 坐标将多边形切为多条水平带（四边形）
        // ══════════════════════════════════════════════════════

        private static List<List<Vec2>> SplitHorizontal(List<Vec2> outline)
        {
            // 1. 收集所有唯一 Y 值并按升序排列
            var ys = outline.Select(p => p.Y).Distinct().OrderBy(y => y).ToList();
            if (ys.Count < 2)
                return new List<List<Vec2>> { outline };

            var strips = new List<List<Vec2>>();

            // 2. 对每段 [yBot, yTop] 构建一条水平带
            for (int i = 0; i < ys.Count - 1; i++)
            {
                float yBot = ys[i];
                float yTop = ys[i + 1];

                if (MathF.Abs(yTop - yBot) < Epsilon)
                    continue;

                // 计算多边形与该水平带的交线，取左右最极值 X
                var xs = new List<float>();
                for (int j = 0; j < outline.Count; j++)
                {
                    Vec2 a = outline[j];
                    Vec2 b = outline[(j + 1) % outline.Count];

                    IntersectEdgeX(a, b, yBot, xs);
                    IntersectEdgeX(a, b, yTop, xs);
                }

                if (xs.Count < 2) continue;

                float minX = xs.Min();
                float maxX = xs.Max();

                // 构建四边形（逆时针）
                strips.Add(new List<Vec2>
                {
                    new Vec2(minX, yBot),
                    new Vec2(maxX, yBot),
                    new Vec2(maxX, yTop),
                    new Vec2(minX, yTop)
                });
            }

            return strips;
        }

        /// <summary>
        /// 线段 ab 与水平线 Y=y 的交点 X 坐标
        /// </summary>
        private static void IntersectEdgeX(Vec2 a, Vec2 b, float y, List<float> results)
        {
            float minY = MathF.Min(a.Y, b.Y);
            float maxY = MathF.Max(a.Y, b.Y);

            // 不在 Y 范围内
            if (y < minY - Epsilon || y > maxY + Epsilon) return;

            // 水平边：两端点 X 都在交线上
            if (MathF.Abs(b.Y - a.Y) < Epsilon)
            {
                results.Add(a.X);
                results.Add(b.X);
                return;
            }

            float t = (y - a.Y) / (b.Y - a.Y);
            float x = a.X + t * (b.X - a.X);
            results.Add(x);
        }
    }
}
