using BimWallData.Public;
using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimAacWallElevationDtoV000
    {
        [JsonProperty("contour")]
        public List<PointXyDto> Contour { get; set; }
    }
}
