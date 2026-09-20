using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimStudDtoV002
    {
        [JsonProperty("holeDiameter")]
        public float HoleDiameter { get; set; }

        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("points")]
        public List<PointXyzDto>? Points { get; set; }
    }
}
