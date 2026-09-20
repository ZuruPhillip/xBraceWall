using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimTensionTieDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("diameter")]
        public float Diameter { get; set; }

        [JsonProperty("length")]
        public float Length { get; set; }
    }
}
