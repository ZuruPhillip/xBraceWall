using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimFinishCutoutDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("point")]
        public PointXyzDto? Point { get; set; }
    }
}
