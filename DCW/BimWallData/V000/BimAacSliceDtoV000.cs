using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimAacSliceDtoV000
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("sliceColumn")]
        public int SliceColumn { get; set; }
        [JsonProperty("contour")]
        public List<PointXyDto> Contour { get; set; }
        [JsonProperty("glueSegments")]
        public List<BimGlueSegmentDtoV000> GlueSegments { get; set; }
    }
}
