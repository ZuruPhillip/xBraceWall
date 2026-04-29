using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimMepCablePointXyDto
    {
        public bool FrontFace { get; set; }
        public PointXyDto? Position { get; set; }
        public string? Type { get; set; }
    }
}
