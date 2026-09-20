using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimOpeningHoleDtoV002
    {
        [JsonProperty("uuid")]
        public string? Uuid { get; set; }

        [JsonProperty("contour")]
        public List<PointXyzDto>? Contour { get; set; }
    }
}
