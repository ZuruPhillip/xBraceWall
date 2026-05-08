using Newtonsoft.Json;
using System.Numerics;

namespace BimWallData.V000
{
    public class GlueSegmentDtoV000
    {
        [JsonProperty("start")]
        public Vector2 Start { get; set; }

        [JsonProperty("end")]
        public Vector2 End { get; set; }
    }
}
