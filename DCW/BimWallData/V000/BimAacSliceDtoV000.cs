using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimAacSliceDtoV000
    {
        public string Id { get; set; }
        public int SliceColumn { get; set; }
        public List<PointXyDto> Contour { get; set; }
        public List<BimGlueSegmentDtoV000> GlueSegments { get; set; }
    }
}
