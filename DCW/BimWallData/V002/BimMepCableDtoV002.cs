using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimMepCableDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("length")]
        public float Length { get; set; }

        [JsonProperty("sn")]
        public int Sn { get; set; }

        [JsonProperty("points")]
        public List<BimMepCablePointXyzDtoV002>? Points { get; set; }
    }
}
