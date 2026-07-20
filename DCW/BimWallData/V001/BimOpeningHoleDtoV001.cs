using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimOpeningHoleDtoV001
    {
        [JsonProperty("uuid")]
        public string? Uuid { get; set; }
        [JsonProperty("contour")]
        public List<PointXyzDto>? Contour { get; set; }
    }
}
