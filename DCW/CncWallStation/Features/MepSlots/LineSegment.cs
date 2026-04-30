using CncWallStation.Transforms;
using Infrastructure.Maths;

namespace CncWallStation.Features.MepSlots
{
    /// <summary>
    /// 直线段
    /// </summary>
    public class LineSegment : ISegment
    {
        // ── 属性 ─────────────────────────────────────────────

        public SegmentType Type => SegmentType.Line;
        public Vec2 StartPoint { get; private set; }
        public Vec2 EndPoint { get; private set; }
        public float Depth { get; set; }

        /// <summary>方向向量（单位向量）</summary>
        public Vec2 Direction => (EndPoint - StartPoint).Normalize();

        /// <summary>线段长度</summary>
        public float Length => (EndPoint - StartPoint).Length();

        // ── 构造 ─────────────────────────────────────────────

        public LineSegment(Vec2 start, Vec2 end, float depth)
        {
            StartPoint = start;
            EndPoint = end;
            Depth = depth;
        }

        // ── 实现接口 ─────────────────────────────────────────

        public ISegment Remap(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            return new LineSegment(
                FlipRemapper.RemapPoint(StartPoint, axis, bounds),
                FlipRemapper.RemapPoint(EndPoint, axis, bounds),
                Depth);
        }

        public Vec2[] Tessellate(int segments = 16)
        {
            // 直线只需起终点
            return new[] { StartPoint, EndPoint };
        }

        public string GetInfo() =>
            $"Line  Start={StartPoint}  End={EndPoint}  " +
            $"L={Length:F2}mm  D={Depth:F2}mm";
    }
}
