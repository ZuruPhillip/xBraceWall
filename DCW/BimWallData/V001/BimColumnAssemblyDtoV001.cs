using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimColumnAssemblyDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("height")]
        public float Height { get; set; }
        [JsonProperty("origin")]
        public PointXyzDto? Origin { get; set; }
    }
}
