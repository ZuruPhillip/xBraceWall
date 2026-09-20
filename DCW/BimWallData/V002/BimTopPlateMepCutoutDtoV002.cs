using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimTopPlateMepCutoutDtoV002
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("points")]
        public List<PointXyDto>? Points { get; set; }
    }
}
