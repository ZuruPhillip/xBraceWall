using CncWallStation.Transforms;
using Infrastructure.Maths;

namespace CncWallStation.Features
{
    /// <summary>
    /// 切槽特征（直线型刀路）
    /// </summary>
    public class Groove : Feature
    {
        /// <summary>槽起点（局部坐标）</summary>
        public Vec2 StartPt { get; set; }

        /// <summary>槽终点（局部坐标）</summary>
        public Vec2 EndPt { get; set; }

        /// <summary>槽宽（mm）</summary>
        public float Width { get; set; }

        /// <summary>槽长（自动计算）</summary>
        public float Length => (EndPt - StartPt).Length();

        public Groove(string id, MachineSide side,
                      Vec2 startPt, Vec2 endPt,
                      float width, float depth)
            : base(id, FeatureType.Groove, side, startPt, depth)
        {
            StartPt = startPt;
            EndPt = endPt;
            Width = width;
            LocalPos = startPt;
        }

        /// <summary>翻面时起点和终点都需要重映射</summary>
        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            StartPt = FlipRemapper.RemapPoint(StartPt, axis, bounds);
            EndPt = FlipRemapper.RemapPoint(EndPt, axis, bounds);
            LocalPos = StartPt;
            Face.ApplyFlipSide(
                FlipRemapper.RemapSide(Face.GetCurrentSide(), axis));
        }

        public override string GetInfo() =>
            $"[Groove {Id}] Side={CurrentSide,-6} " +
            $"Start={StartPt} End={EndPt} " +
            $"W={Width}mm D={Depth}mm L={Length:F2}mm";
    }
}
