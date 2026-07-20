using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimMepDeviceDtoV001
    {
        [JsonProperty("frontFace")]
        public bool FrontFace { get; set; }
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("position")]
        public PointXyDto? Position { get; set; }
    }
}
