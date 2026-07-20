using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimShearKeysDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("points")]
        public List<PointXyzDto>? Points { get; set; }
    }
}
