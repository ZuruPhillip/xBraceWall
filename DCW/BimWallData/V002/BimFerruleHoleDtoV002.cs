using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimFerruleHoleDtoV002
    {
        [JsonProperty("ferrulePn")]
        public string? FerrulePn { get; set; }

        [JsonProperty("holeDiameter")]
        public float HoleDiameter { get; set; }

        [JsonProperty("points")]
        public List<PointXyzDto>? Points { get; set; }
    }
}
