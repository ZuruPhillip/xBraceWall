using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimTopPlateDtoV000
    {
        public string? Pn { get; set; }
        public PointXyzDto? StartPoint { get; set; }
        public PointXyzDto? EndPoint { get; set; }
        public PointXyzDto? Position { get; set; }
        public float Width { get; set; }
        public float ProfileThickness { get; set; }
        public BimFerruleHoleDtoV000? FerruleHoles { get; set; }
        public BimStudDtoV000? Studs { get; set; }
    }
}
