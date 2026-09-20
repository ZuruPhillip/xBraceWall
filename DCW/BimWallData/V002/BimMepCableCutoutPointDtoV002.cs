using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimMepCableCutoutPointDtoV002
    {
        [JsonProperty("frontFace")]
        public bool FrontFace { get; set; }

        [JsonProperty("position")]
        public PointXyDto? Position { get; set; }

        [JsonProperty("sn")]
        public int Sn { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }
    }
}
