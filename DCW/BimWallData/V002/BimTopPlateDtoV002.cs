using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimTopPlateDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("startPoint")]
        public PointXyzDto? StartPoint { get; set; }

        [JsonProperty("endPoint")]
        public PointXyzDto? EndPoint { get; set; }

        [JsonProperty("position")]
        public PointXyzDto? Position { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("profileThickness")]
        public float ProfileThickness { get; set; }

        [JsonProperty("ferruleHoles")]
        public BimFerruleHoleDtoV002? FerruleHoles { get; set; }

        [JsonProperty("mepCutouts")]
        public BimTopPlateMepCutoutDtoV002? MepCutouts { get; set; }

        [JsonProperty("studs")]
        public BimStudDtoV002? Studs { get; set; }
    }
}
