using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimAacSliceDtoV001
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("gluePn")]
        public string? GluePn { get; set; }
        [JsonProperty("sliceColumn")]
        public int SliceColumn { get; set; }
        [JsonProperty("contour")]
        public List<PointXyDto> Contour { get; set; }
        [JsonProperty("glueSegments")]
        public List<BimGlueSegmentDtoV001>? GlueSegments { get; set; }
    }
}
