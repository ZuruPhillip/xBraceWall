using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs
{
    public static class GrooveSideClassifier
    {
        /// <summary>相交容差（mm）— 距离墙边小于该值视为贴边</summary>
        private const float EDGE_TOLERANCE = 1.0f;

        /// <summary>
        /// 根据 Groove 与 Wall 的 BoundingBox 相交关系，判断槽所属的侧
        ///
        /// 判定流程：
        ///   1. 求 Groove 4 角点的 AABB
        ///   2. 检查与墙四条边（Top/Bottom/Left/Right）是否相交
        ///   3. 若仅一侧相交 → 返回该侧
        ///   4. 若多侧相交（角部情况）：
        ///      • Groove X 跨度 ≥ Y 跨度 → 横向，归属 Top/Bottom
        ///      • 否则 → 纵向，归属 Left/Right
        ///      然后从相交的多侧中取与该方向匹配的那一侧
        /// </summary>
        public static GrooveSide Classify(Groove groove, MomWall wall)
        {
            // ── 1. 计算 Groove AABB（基于 4 角点）─────────────
            var (p0, p1, p2, p3) = groove.GetCorners();
            float gMinX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
            float gMaxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
            float gMinY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
            float gMaxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

            // ── 2. 墙 AABB ───────────────────────────────────
            var (wMinX, wMinY, wMaxX, wMaxY) = wall.GetOutlineBounds();

            // ── 3. 四条边相交判断（贴边即相交）─────────────────
            bool hitBottom = gMinY <= wMinY + EDGE_TOLERANCE;
            bool hitTop = gMaxY >= wMaxY - EDGE_TOLERANCE;
            bool hitLeft = gMinX <= wMinX + EDGE_TOLERANCE;
            bool hitRight = gMaxX >= wMaxX - EDGE_TOLERANCE;

            int hitCount = (hitBottom ? 1 : 0) + (hitTop ? 1 : 0)
                         + (hitLeft ? 1 : 0) + (hitRight ? 1 : 0);

            if (hitCount == 0) return GrooveSide.None;

            // ── 4. 单边相交：直接返回 ─────────────────────────
            if (hitCount == 1)
            {
                if (hitBottom) return GrooveSide.Bottom;
                if (hitTop) return GrooveSide.Top;
                if (hitLeft) return GrooveSide.Left;
                if (hitRight) return GrooveSide.Right;
            }

            // ── 5. 多边相交：按槽较长边方向决定 ────────────────
            float spanX = gMaxX - gMinX;
            float spanY = gMaxY - gMinY;

            if (spanX >= spanY)
            {
                // 横向槽 → 优先归 Top/Bottom
                if (hitBottom) return GrooveSide.Bottom;
                if (hitTop) return GrooveSide.Top;
                // 兜底（理论上不会到这里）
                if (hitLeft) return GrooveSide.Left;
                if (hitRight) return GrooveSide.Right;
            }
            else
            {
                // 纵向槽 → 优先归 Left/Right
                if (hitLeft) return GrooveSide.Left;
                if (hitRight) return GrooveSide.Right;
                if (hitBottom) return GrooveSide.Bottom;
                if (hitTop) return GrooveSide.Top;
            }

            return GrooveSide.None;
        }

        /// <summary>
        /// 槽相对墙的方位
        /// </summary>
        public enum GrooveSide
        {
            None,
            Top,      // Y = wallMaxY 边
            Bottom,   // Y = wallMinY 边
            Left,     // X = wallMinX 边
            Right,    // X = wallMaxX 边
        }
    }
}
