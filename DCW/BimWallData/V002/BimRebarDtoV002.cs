using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimRebarDtoV002
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
        public List<BimRodDtoV002>? Rods { get; set; }
    }
}
