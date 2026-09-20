using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimAacSliceDtoV002
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
        public List<BimGlueSegmentDtoV002>? GlueSegments { get; set; }
    }
}
