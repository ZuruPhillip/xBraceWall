namespace BimWallData.V000
{
    public class BimWallDtoV000
    {
        public string? Id { get; set; }
        public string? Pn { get; set; }
        public float AacDensity { get; set; }
        public float CoreHeight { get; set; }
        public float CoreThickness { get; set; }
        public string? Schema { get; set; }
        public List<BimAacSliceDtoV000>? AacSlices { get; set; }
        public BimAacWallElevationDtoV000? AacWallElevation { get; set; }
        public BimBendingKeyDtoV000? bendingKeys { get; set; }
        //public List<BimFaceFinishesDtoV000>? FaceFinishes { get; set; }
        public List<BimMepCableDtoV000>? MepCables { get; set; }
        public List<BimMepDeviceDtoV000>? MepDevices { get; set; }
        public List<BimOpeningHoleDtoV000>? OpeningHoles { get; set; }
        public BimRebarDtoV000? Rebars { get; set; }
        public List<BimSteelFrameColumnDtoV000>? SteelFrameColumns { get; set; }
        public BimTensionTieDtoV000? TensionTie { get; set; }
        public List<BimTopPlateDtoV000>? TopPlate { get; set; }
        //public List<BimWaffleSlabLinksDtoV000>? WaffleSlabLinks { get; set; }
        public List<BimXpsDtoV000>? Xps { get; set; }
    }
}
