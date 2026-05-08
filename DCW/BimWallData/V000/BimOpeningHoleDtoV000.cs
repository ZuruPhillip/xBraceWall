using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimOpeningHoleDtoV000
    {
        [JsonProperty("uuid")]
        public string? Uuid { get; set; }
        [JsonProperty("contour")]
        public List<PointXyzDto>? Contour { get; set; }
    }
}
