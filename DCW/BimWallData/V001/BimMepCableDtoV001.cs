using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimMepCableDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("points")]
        public List<BimMepCablePointXyDtoV001>? Points { get; set; }
        [JsonProperty("hash")]
        public long Hash { get; set; }
    }
}
