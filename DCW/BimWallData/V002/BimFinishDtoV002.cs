using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimFinishDtoV002
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
        public List<BimFinishCutoutDtoV002>? Cutouts { get; set; }
    }
}
