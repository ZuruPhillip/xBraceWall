using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimWallDtoV001 : BimWallDtoBase
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("aacDensity")]
        public float AacDensity { get; set; }
        [JsonProperty("coreHeight")]
        public float CoreHeight { get; set; }

        [JsonProperty("aacSlices")]
        public List<BimAacSliceDtoV001>? AacSlices { get; set; }

        [JsonProperty("columnAssemblies")]
        public List<BimColumnAssemblyDtoV001>? ColumnAssemblies { get; set; }

        [JsonProperty("faceFinishes")]
        public List<BimFaceFinishDtoV001>? FaceFinishes { get; set; }

        [JsonProperty("mepCables")]
        public List<BimMepCableDtoV001>? MepCables { get; set; }
        [JsonProperty("mepDevices")]
        public List<BimMepDeviceDtoV001>? MepDevices { get; set; }
        [JsonProperty("openingHoles")]
        public List<BimOpeningHoleDtoV001>? OpeningHoles { get; set; }

        [JsonProperty("proppingConnectors")]
        public BimProppingConnectorsDtoV001? ProppingConnectors { get; set; }

        [JsonProperty("rebars")]
        public BimRebarDtoV001? Rebars { get; set; }

        [JsonProperty("shearKeys")]
        public BimShearKeysDtoV001? ShearKeys { get; set; }

        [JsonProperty("tensionTie")]
        public BimTensionTieDtoV001? TensionTie { get; set; }

        [JsonProperty("topPlate")]
        public List<BimTopPlateDtoV001>? TopPlate { get; set; }

        [JsonProperty("waffleSlabLinks")]
        public List<BimWaffleSlabLinkDtoV001>? WaffleSlabLinks { get; set; }

        [JsonProperty("xps")]
        public List<BimXpsDtoV001>? Xps { get; set; }
    }
}
