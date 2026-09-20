using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimWallDtoV002 : BimWallDtoBase
    {
        [JsonProperty("pn")]
        public string? Pn { get; set; }

        [JsonProperty("aacDensity")]
        public float AacDensity { get; set; }

        [JsonProperty("coreHeight")]
        public float CoreHeight { get; set; }

        [JsonProperty("aacSlices")]
        public List<BimAacSliceDtoV002>? AacSlices { get; set; }

        [JsonProperty("columnAssemblies")]
        public List<BimColumnAssemblyDtoV002>? ColumnAssemblies { get; set; }

        [JsonProperty("faceFinishes")]
        public List<BimFaceFinishDtoV002>? FaceFinishes { get; set; }

        [JsonProperty("mepCables")]
        public List<BimMepCableDtoV002>? MepCables { get; set; }

        [JsonProperty("mepDevices")]
        public List<BimMepDeviceDtoV002>? MepDevices { get; set; }

        [JsonProperty("mepCableCutouts")]
        public List<BimMepCableCutoutDtoV002>? MepCableCutouts { get; set; }

        [JsonProperty("openingHoles")]
        public List<BimOpeningHoleDtoV002>? OpeningHoles { get; set; }

        [JsonProperty("proppingConnectors")]
        public BimProppingConnectorsDtoV002? ProppingConnectors { get; set; }

        [JsonProperty("rebars")]
        public BimRebarDtoV002? Rebars { get; set; }

        [JsonProperty("shearKeys")]
        public BimShearKeysDtoV002? ShearKeys { get; set; }

        [JsonProperty("tensionTie")]
        public BimTensionTieDtoV002? TensionTie { get; set; }

        [JsonProperty("topPlate")]
        public List<BimTopPlateDtoV002>? TopPlate { get; set; }

        [JsonProperty("waffleSlabLinks")]
        public List<BimWaffleSlabLinkDtoV002>? WaffleSlabLinks { get; set; }

        [JsonProperty("xps")]
        public List<BimXpsDtoV002>? Xps { get; set; }
    }
}
