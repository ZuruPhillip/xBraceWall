using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimWaffleSlabLinkDtoV002
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("side")]
        public string? Side { get; set; }
    }
}
