using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimTensionTieDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("diameter")]
        public float Diameter { get; set; }
        [JsonProperty("length")]
        public float Length { get; set; }
    }
}
