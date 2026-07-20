using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimBeautySealantDtoV001
    {
        [JsonProperty("colour")]
        public string? Colour { get; set; }
        [JsonProperty("size")]
        public float Size { get; set; }
    }
}
