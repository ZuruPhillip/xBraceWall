namespace BimWallData.V000
{
    public class BimRebarDtoV000
    {
        public float Diameter { get; set; }
        public float HorizontalDepth { get; set; }
        public float VerticalDepth { get; set; }
        public string? Pn { get; set; }
        public List<BimRodDtoV000>? Rods { get; set; }
    }
}
