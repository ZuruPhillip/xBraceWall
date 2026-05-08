using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimRebarDtoV000
    {
        [JsonProperty("diameter")]
        public float Diameter { get; set; }
        [JsonProperty("horizontalDepth")]
        public float HorizontalDepth { get; set; }
        [JsonProperty("verticalDepth")]
        public float VerticalDepth { get; set; }
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("rods")]
        public List<BimRodDtoV000>? Rods { get; set; }
    }
}
