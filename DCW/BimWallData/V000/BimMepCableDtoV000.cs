using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimMepCableDtoV000
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("points")]
        public List<BimMepCablePointXyDto>? Points { get; set; }
        [JsonProperty("hash")]
        public long Hash { get; set; }
    }
}
