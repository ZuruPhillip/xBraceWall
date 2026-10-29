using Infrastructure.Maths;

namespace CncWallStation.Features
{
    /// <summary>
    /// 矩形挖槽 / 开槽特征
    /// </summary>
    public class Pocket : Feature
    {
        /// <summary>挖槽类型（业务分类）</summary>
        public PocketType PocketType { get; set; }

        /// <summary>口袋长度（mm）</summary>
        public float Length { get; set; }

        /// <summary>口袋宽度（mm）</summary>
        public float Width { get; set; }

        /// <summary>圆角半径（0 = 直角）</summary>
        public float CornerRadius { get; set; }

        public Pocket(string id, MachineSide side,
                      Vec2 center, float width, float length,
                      float depth, float cornerRadius = 0f,
                      PocketType pocketType = PocketType.General)
            : base(id, FeatureType.Pocket, side, center, depth)
        {
            Width = width;
            Length = length;
            CornerRadius = cornerRadius;
            PocketType = pocketType;
        }

        public Pocket() : base() { }
        public override string GetInfo() =>
            $"[Pocket {Id}] Type={PocketType,-20} " +
            $"Side={CurrentSide,-6} " +
            $"Center={LocalPos} " +
            $"W={Width}mm H={Length}mm D={Depth}mm CR={CornerRadius}mm";
    }
}
