using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimBeautySealantDtoV002
    {
        [JsonProperty("colour")]
        public string? Colour { get; set; }

        [JsonProperty("size")]
        public float Size { get; set; }
    }
}
