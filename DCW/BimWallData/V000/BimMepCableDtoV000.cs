namespace BimWallData.V000
{
    public class BimMepCableDtoV000
    {
        public string? Pn { get; set; }
        public List<BimMepCablePointXyDto>? Points { get; set; }
        public long Hash { get; set; }
    }
}
