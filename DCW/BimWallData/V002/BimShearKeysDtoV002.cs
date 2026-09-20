using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimShearKeysDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("points")]
        public List<PointXyzDto>? Points { get; set; }
    }
}
