using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public sealed class BimGlueSegmentDtoV001
    {
        [JsonProperty("startPoint")]
        public PointXyDto StartPoint { get; set; }

        [JsonProperty("endPoint")]
        public PointXyDto EndPoint { get; set; }
    }
}
