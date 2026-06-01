using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features.MepSlots
{
    /// <summary>
    /// 圆弧段
    /// 使用「圆心 + 起始角 + 终止角 + 半径」定义
    /// 支持顺时针 / 逆时针方向
    /// </summary>
    public class ArcSegment : ISegment
    {
        // ── 属性 ─────────────────────────────────────────────

        public SegmentType Type => SegmentType.Arc;
        public float Depth { get; set; }

        /// <summary>圆心（局部坐标）</summary>
        [JsonPropertyName("center")]
        public Vec2 Center { get;  set; }
        [JsonPropertyName("radius")]
        /// <summary>半径（mm）</summary>
        public float Radius { get;  set; }

        /// <summary>起始角度（弧度，从+X轴顺时针量）</summary>
        [JsonIgnore]
        public float StartAngle { get;  set; }

        /// <summary>终止角度（弧度）</summary>
        [JsonIgnore]
        public float EndAngle { get;  set; }

        /// <summary>是否顺时针（CW = true，CCW = false）</summary>
        public bool IsClockwise { get;  set; }

        public float? OverrideWidth { get; set; } = null;
        // ── 派生属性 ─────────────────────────────────────────
        /// <summary>
        /// 起始角度（度，序列化用）
        /// 内部存弧度，对外暴露角度便于阅读
        /// </summary>
        [JsonPropertyName("StartAngleDeg")]
        public float StartAngleDeg
        {
            get => StartAngle * 180f / MathF.PI;
            set => StartAngle = value * MathF.PI / 180f;
        }

        /// <summary>终止角度（度，序列化用）</summary>
        [JsonPropertyName("EndAngleDeg")]
        public float EndAngleDeg
        {
            get => EndAngle * 180f / MathF.PI;
            set => EndAngle = value * MathF.PI / 180f;
        }

        public Vec2 StartPoint =>
            Center + new Vec2(
                Radius * MathF.Cos(StartAngle),
                Radius * MathF.Sin(StartAngle));

        public Vec2 EndPoint =>
            Center + new Vec2(
                Radius * MathF.Cos(EndAngle),
                Radius * MathF.Sin(EndAngle));

        /// <summary>扫过角度（始终为正值）</summary>
        [JsonIgnore]
        public float SweepAngle
        {
            get
            {
                float delta = EndAngle - StartAngle;
                if (IsClockwise)
                {
                    // 顺时针：delta 应为负
                    if (delta > 0) delta -= MathF.PI * 2f;
                }
                else
                {
                    // 逆时针：delta 应为正
                    if (delta < 0) delta += MathF.PI * 2f;
                }
                return MathF.Abs(delta);
            }
        }

        /// <summary>弧长</summary>
        [JsonIgnore]
        public float Length => Radius * SweepAngle;

        // ── 构造 ─────────────────────────────────────────────

        public ArcSegment() { }

        [JsonConstructor]
        public ArcSegment(Vec2 center,
                          float radius,
                          float startAngleDeg,
                          float endAngleDeg,
                          float depth,
                          bool isClockwise = false)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngleDeg * MathF.PI / 180f;
            EndAngle = endAngleDeg * MathF.PI / 180f;
            Depth = depth;
            IsClockwise = isClockwise;
        }

        // 内部构造（弧度）
        private ArcSegment(Vec2 center, float radius,
                           float startRad, float endRad,
                           float depth, bool cw, bool _useRad)
        {
            Center = center;
            Radius = radius;
            StartAngle = startRad;
            EndAngle = endRad;
            Depth = depth;
            IsClockwise = cw;
        }

        // ── 工厂方法 ─────────────────────────────────────────

        /// <summary>
        /// 从三点构造圆弧（起点、弧上一点、终点）
        /// </summary>
        public static ArcSegment FromThreePoints(
            Vec2 p1, Vec2 pMid, Vec2 p3, float depth)
        {
            // 求外接圆圆心
            float ax = p1.X, ay = p1.Y;
            float bx = pMid.X, by = pMid.Y;
            float cx = p3.X, cy = p3.Y;

            float d = 2f * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (MathF.Abs(d) < 1e-6f)
                throw new InvalidOperationException("三点共线，无法构造圆弧");

            float ux = ((ax * ax + ay * ay) * (by - cy)
                       + (bx * bx + by * by) * (cy - ay)
                       + (cx * cx + cy * cy) * (ay - by)) / d;
            float uy = ((ax * ax + ay * ay) * (cx - bx)
                       + (bx * bx + by * by) * (ax - cx)
                       + (cx * cx + cy * cy) * (bx - ax)) / d;

            var center = new Vec2(ux, uy);
            float radius = (p1 - center).Length();

            float sa = MathF.Atan2(ay - uy, ax - ux);
            float ea = MathF.Atan2(cy - uy, cx - ux);

            // 判断方向（叉积判断中间点是否在起终点逆时针弧上）
            float cross = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            bool cw = cross < 0;

            return new ArcSegment(center, radius, sa, ea, depth, cw, true);
        }

        /// <summary>
        /// 从相切方向构造圆弧（起点、起始切线方向、终点）
        /// </summary>
        public static ArcSegment FromTangent(
            Vec2 start, Vec2 tangentDir, Vec2 end, float depth)
        {
            // 切线方向与起终点中垂线的交点即圆心
            Vec2 mid = new Vec2((start.X + end.X) * 0.5f, (start.Y + end.Y) * 0.5f);
            Vec2 chord = end - start;
            Vec2 perpChord = new Vec2(-chord.Y, chord.X).Normalize();
            Vec2 tang = tangentDir.Normalize();
            Vec2 perpTang = new Vec2(-tang.Y, tang.X);

            // 求直线交点：start + t*perpTang = mid + s*perpChord
            float denom = perpTang.Cross(perpChord);
            if (MathF.Abs(denom) < 1e-6f)
                throw new InvalidOperationException("切线与弦平行，无法确定圆心");

            float t = (mid - start).Cross(perpChord) / denom;
            Vec2 center = start + perpTang * t;
            float radius = (start - center).Length();

            float sa = MathF.Atan2(start.Y - center.Y, start.X - center.X);
            float ea = MathF.Atan2(end.Y - center.Y, end.X - center.X);

            float cross = tang.Cross((end - start).Normalize());
            bool cw = cross < 0;

            return new ArcSegment(center, radius, sa, ea, depth, cw, true);
        }

        // ── 实现接口 ─────────────────────────────────────────

        public ISegment Remap(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            Vec2 newCenter = FlipRemapper.RemapPoint(Center, axis, bounds);

            // 翻面后角度镜像
            float newStartAngle, newEndAngle;
            bool newCW;

            switch (axis)
            {
                case FlipAxis.AroundX:
                // Y 轴镜像：角度关于 X 轴取反
                newStartAngle = -StartAngle;
                newEndAngle = -EndAngle;
                newCW = !IsClockwise;
                break;

                case FlipAxis.AroundY:
                // X 轴镜像：角度关于 Y 轴取反 (π - angle)
                newStartAngle = MathF.PI - StartAngle;
                newEndAngle = MathF.PI - EndAngle;
                newCW = !IsClockwise;
                break;

                case FlipAxis.AroundZ:
                // XY 都镜像：角度 + π
                newStartAngle = StartAngle + MathF.PI;
                newEndAngle = EndAngle + MathF.PI;
                newCW = IsClockwise;
                break;

                default:
                newStartAngle = StartAngle;
                newEndAngle = EndAngle;
                newCW = IsClockwise;
                break;
            }

            return new ArcSegment(newCenter, Radius,
                                  newStartAngle, newEndAngle,
                                  Depth, newCW, true);
        }

        public Vec2[] Tessellate(int steps = 16)
        {
            var pts = new Vec2[steps + 1];
            float sweep = IsClockwise ? -SweepAngle : SweepAngle;

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float angle = StartAngle + sweep * t;
                pts[i] = new Vec2(
                    Center.X + Radius * MathF.Cos(angle),
                    Center.Y + Radius * MathF.Sin(angle));
            }
            return pts;
        }

        public string GetInfo()
        {
            float startDeg = StartAngle * 180f / MathF.PI;
            float endDeg = EndAngle * 180f / MathF.PI;
            string dir = IsClockwise ? "CW" : "CCW";
            return $"Arc   Center={Center}  R={Radius:F2}mm  " +
                   $"{startDeg:F1}°→{endDeg:F1}°({dir})  " +
                   $"L={Length:F2}mm  D={Depth:F2}mm";
        }
    }
}
