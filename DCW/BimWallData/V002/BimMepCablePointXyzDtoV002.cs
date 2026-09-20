using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimMepCablePointXyzDtoV002
    {
        [JsonProperty("sn")]
        public int Sn { get; set; }

        [JsonProperty("position")]
        public PointXyzDto? Position { get; set; }
    }
}
