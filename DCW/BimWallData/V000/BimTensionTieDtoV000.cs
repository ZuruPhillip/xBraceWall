using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimTensionTieDtoV000
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("diameter")]
        public float Diameter { get; set; }
        [JsonProperty("length")]
        public float Length { get; set; }
    }
}
