using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimRebarDtoV001
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
        public List<BimRodDtoV001>? Rods { get; set; }
    }
}
