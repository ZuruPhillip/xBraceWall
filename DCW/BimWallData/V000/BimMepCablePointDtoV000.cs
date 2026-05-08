using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimMepCablePointXyDto
    {
        [JsonProperty("frontFace")]
        public bool FrontFace { get; set; }
        [JsonProperty("position")]
        public PointXyDto? Position { get; set; }
        [JsonProperty("type")]
        public string? Type { get; set; }
    }
}
