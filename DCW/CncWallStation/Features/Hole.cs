using Infrastructure.Maths;

namespace CncWallStation.Features
{
    /// <summary>
    /// 圆形开孔特征
    /// </summary>
    public class Hole : Feature
    {
        /// <summary>孔半径（mm）</summary>
        public float Radius { get; set; }

        /// <summary>是否通孔</summary>
        public bool ThroughHole { get; set; }

        /// <summary>孔直径</summary>
        public float Diameter => Radius * 2f;

        public Hole(string id, MachineSide side,
                    Vec2 center, float radius, float depth,
                    bool throughHole = false)
            : base(id, FeatureType.Hole, side, center, depth)
        {
            Radius = radius;
            ThroughHole = throughHole;
        }

        public override string GetInfo() =>
            $"[Hole   {Id}] Side={CurrentSide,-6} " +
            $"Center={LocalPos} R={Radius}mm D={Depth}mm " +
            $"{(ThroughHole ? "[通孔]" : "[盲孔]")}";
    }
}
