using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimBaseBracketDtoV000
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("pinPn")]
        public string? pinPN { get; set; }
        [JsonProperty("pinNumber")]
        public int PinNumber { get; set; }
    }
}
