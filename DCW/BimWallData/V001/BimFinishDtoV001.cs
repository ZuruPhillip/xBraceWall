using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimFinishDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("thickness")]
        public float Thickness { get; set; }
        [JsonProperty("glueThickness")]
        public float GlueThickness { get; set; }
        [JsonProperty("artwork")]
        public string? Artwork { get; set; }
        [JsonProperty("contour")]
        public List<PointXyzDto>? Contour { get; set; }
        [JsonProperty("cutouts")]
        public string? Cutouts { get; set; }
    }
}
