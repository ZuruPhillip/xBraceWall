using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimProppingConnectorItemDtoV001
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("position")]
        public PointXyzDto? Position { get; set; }
    }
}
