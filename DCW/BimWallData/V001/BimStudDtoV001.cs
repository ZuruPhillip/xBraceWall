using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimStudDtoV001
    {
        [JsonProperty("holeDiameter")]
        public float HoleDiameter { get; set; }
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("points")]
        public List<PointXyzDto>? Points { get; set; }
    }
}
