using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimRodDtoV000
    {
        public PointXyzDto StartPoint { get; set; }
        public PointXyzDto EndPoint { get; set; }
        public bool StartThreading { get; set; }
        public bool EndThreading { get; set; }
    }
}
