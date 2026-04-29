using BimWallData.Public;

namespace BimWallData.V000
{
    public class BimSteelFrameColumnDtoV000
    {
        public string? Pn { get; set; }
        public PointXyzDto? StartPoint { get; set; }
        public PointXyzDto? EndPoint { get; set; }
        public int LeftRightSide { get; set; }//1 left; 2 middle; 3 right
        public int FrontBackSide { get; set; }//1 front; 2 back
        public float Height { get; set; }
        public float ProfileSize { get; set; }
        public float ProfileThickness { get; set; }
        public float Rotation { get; set; }
        public BimBaseBracketDtoV000? BaseBracket { get; set; }
        public BimTopBracketDtoV000? TopBracket { get; set; }
        public string XpsPn { get; set; }
    }
}
