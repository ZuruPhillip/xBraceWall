using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimRodDtoV001
    {
        [JsonProperty("startPoint")]
        public PointXyzDto StartPoint { get; set; }
        [JsonProperty("endPoint")]
        public PointXyzDto EndPoint { get; set; }
        [JsonProperty("startThreading")]
        public bool StartThreading { get; set; }
        [JsonProperty("endThreading")]
        public bool EndThreading { get; set; }
    }
}
