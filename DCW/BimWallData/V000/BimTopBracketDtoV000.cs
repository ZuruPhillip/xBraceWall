using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimTopBracketDtoV000
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("pinPn")]
        public string? PinPN { get; set; }
        [JsonProperty("pinNumber")]
        public int PinNumber { get; set; }
        [JsonProperty("isFlipped")]
        public bool IsFlipped { get; set; }
    }
}
