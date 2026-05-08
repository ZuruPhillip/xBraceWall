using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public sealed class BimGlueSegmentDtoV000
    {
        [JsonProperty("startPoint")]
        public PointXyDto StartPoint { get; set; }

        [JsonProperty("endPoint")]
        public PointXyDto EndPoint { get; set; }
    }
}
