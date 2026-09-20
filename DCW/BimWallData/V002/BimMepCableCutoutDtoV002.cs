using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimMepCableCutoutDtoV002
    {
        [JsonProperty("depth")]
        public float Depth { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("sn")]
        public int Sn { get; set; }

        [JsonProperty("points")]
        public List<BimMepCableCutoutPointDtoV002>? Points { get; set; }
    }
}
