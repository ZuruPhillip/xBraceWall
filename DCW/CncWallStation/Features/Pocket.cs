using Infrastructure.Maths;

namespace CncWallStation.Features
{
    /// <summary>
    /// 矩形挖坑 / 开槽特征
    /// </summary>
    public class Pocket : Feature
    {
        /// <summary>口袋长度（mm）</summary>
        public float Length { get; set; }

        /// <summary>口袋宽度（mm）</summary>
        public float Width { get; set; }

        /// <summary>圆角半径（0 = 直角）</summary>
        public float CornerRadius { get; set; }

        public Pocket(string id, MachineSide side,
                      Vec2 center, float width, float length,
                      float depth, float cornerRadius = 0f)
            : base(id, FeatureType.Pocket, side, center, depth)
        {
            Width = width;
            Length = length;
            CornerRadius = cornerRadius;
        }

        public Pocket() : base() { }
        public override string GetInfo() =>
            $"[Pocket {Id}] Side={CurrentSide,-6} " +
            $"Center={LocalPos} " +
            $"W={Width}mm H={Length}mm D={Depth}mm CR={CornerRadius}mm";
    }
}
