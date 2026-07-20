using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimTopPlateDtoV001
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
        public BimFerruleHoleDtoV001? FerruleHoles { get; set; }
        [JsonProperty("studs")]
        public BimStudDtoV001? Studs { get; set; }
    }
}
