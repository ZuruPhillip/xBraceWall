using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimTopPlateDtoV000
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
        public BimFerruleHoleDtoV000? FerruleHoles { get; set; }
        [JsonProperty("studs")]
        public BimStudDtoV000? Studs { get; set; }
    }
}
