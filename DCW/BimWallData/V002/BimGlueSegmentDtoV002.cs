using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V002
{
    public sealed class BimGlueSegmentDtoV002
    {
        [JsonProperty("startPoint")]
        public PointXyDto StartPoint { get; set; }

        [JsonProperty("endPoint")]
        public PointXyDto EndPoint { get; set; }
    }
}
