using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimFaceFinishDtoV002
    {
        [JsonProperty("beautySealant")]
        public BimBeautySealantDtoV002? BeautySealant { get; set; }

        [JsonProperty("finishes")]
        public List<BimFinishDtoV002>? Finishes { get; set; }
    }
}
