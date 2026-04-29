using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimOpeningHoleDtoV000
    {
        public string? Uuid { get; set; }
        public List<PointXyzDto>? Contour { get; set; }
    }
}
