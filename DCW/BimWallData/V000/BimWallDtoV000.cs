using Newtonsoft.Json;

namespace BimWallData.V000
{
    public class BimWallDtoV000 : BimWallDtoBase
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }
        [JsonProperty("aacDensity")]
        public float AacDensity { get; set; }
        [JsonProperty("coreHeight")]
        public float CoreHeight { get; set; }
        
        [JsonProperty("aacSlices")]
        public List<BimAacSliceDtoV000>? AacSlices { get; set; }
        
        [JsonProperty("bendingKeys")]
        public BimBendingKeyDtoV000? BendingKeys { get; set; }
        [JsonProperty("mepCables")]
        public List<BimMepCableDtoV000>? MepCables { get; set; }
        [JsonProperty("mepDevices")]
        public List<BimMepDeviceDtoV000>? MepDevices { get; set; }
        [JsonProperty("openingHoles")]
        public List<BimOpeningHoleDtoV000>? OpeningHoles { get; set; }
        [JsonProperty("rebars")]
        public BimRebarDtoV000? Rebars { get; set; }
        [JsonProperty("steelFrameColumns")]
        public List<BimSteelFrameColumnDtoV000>? SteelFrameColumns { get; set; }
        [JsonProperty("tensionTie")]
        public BimTensionTieDtoV000? TensionTie { get; set; }
        [JsonProperty("topPlate")]
        public List<BimTopPlateDtoV000>? TopPlate { get; set; }
        [JsonProperty("xps")]
        public List<BimXpsDtoV000>? Xps { get; set; }
    }
}
