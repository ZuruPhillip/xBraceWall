using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimWaffleSlabLinkDtoV001
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
        [JsonProperty("side")]
        public string? Side { get; set; }
    }
}
