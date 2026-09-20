using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimMepDeviceDtoV002
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("position")]
        public PointXyzDto? Position { get; set; }
    }
}
