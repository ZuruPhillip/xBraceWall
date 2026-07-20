using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimFaceFinishDtoV001
    {
        [JsonProperty("beautySealant")]
        public BimBeautySealantDtoV001? BeautySealant { get; set; }

        [JsonProperty("finishes")]
        public List<BimFinishDtoV001>? Finishes { get; set; }
    }
}
