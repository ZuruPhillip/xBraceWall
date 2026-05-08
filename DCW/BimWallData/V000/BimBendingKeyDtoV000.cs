using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimBendingKeyDtoV000
    {
        [JsonProperty("pn")]
        public string Pn { get; set; }
        [JsonProperty("points")]
        public List<PointXyDto> Points { get; set; }
    }
}
