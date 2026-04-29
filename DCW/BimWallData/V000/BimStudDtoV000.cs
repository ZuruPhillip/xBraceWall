using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimStudDtoV000
    {
        public float HoleDiameter { get; set; }
        public string? Pn { get; set; }
        public List<PointXyzDto>? Points { get; set; }
    }
}
