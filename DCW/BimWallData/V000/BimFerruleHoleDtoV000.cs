using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimFerruleHoleDtoV000
    {
        public string? FerrulePn { get; set; }
        public float HoleDiameter { get; set; }
        public List<PointXyzDto>? Points { get; set; }
    }
}
