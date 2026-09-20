using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimXpsDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }

        [JsonProperty("position")]
        public PointXyzDto? Position { get; set; }
    }
}
