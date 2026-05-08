using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimSteelFrameColumnDtoV000
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("startPoint")]
        public PointXyzDto? StartPoint { get; set; }
        [JsonProperty("endPoint")]
        public PointXyzDto? EndPoint { get; set; }
        [JsonProperty("leftRightSide")]
        public int LeftRightSide { get; set; }//1 left; 2 middle; 3 right
        [JsonProperty("frontBackSide")]
        public int FrontBackSide { get; set; }//1 front; 2 back
        [JsonProperty("height")]
        public float Height { get; set; }
        [JsonProperty("profileSize")]
        public float ProfileSize { get; set; }
        [JsonProperty("profileThickness")]
        public float ProfileThickness { get; set; }
        [JsonProperty("rotation")]
        public float Rotation { get; set; }
        [JsonProperty("baseBracket")]
        public BimBaseBracketDtoV000? BaseBracket { get; set; }
        [JsonProperty("topBracket")]
        public BimTopBracketDtoV000? TopBracket { get; set; }
        [JsonProperty("xpsPn")]
        public string XpsPn { get; set; }
    }
}
