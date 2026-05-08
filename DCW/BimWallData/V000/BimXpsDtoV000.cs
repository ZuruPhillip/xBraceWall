using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimXpsDtoV000
    {
        [JsonProperty("pn")]
        public string Pn { get; set; }
        [JsonProperty("leftRightSide")]
        public int LeftRightSide { get; set; } // 1 left; 2 middle; 3 right
        [JsonProperty("type")]
        public int Type { get; set; } // 1 200-straight; 2 200-corner; 3 300 straight 4. 300 corner 5.200-300 corner 6 300-200 corner
        [JsonProperty("position")]
        public PointXyzDto Position { get; set; }
    }
}
