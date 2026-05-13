using CncWallStation.Features;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs.Handlers
{
    public static class WallHandler
    {
        public static void Handle(MomWall wall, PlcConvertContext ctx)
        {
            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.Mill,
                F = PlcFeatureCode.WallDefine,
                D = 0,
                X0 = wall.ActualLength,
                Y0 = wall.ActualWidth,
                Z0 = wall.ActualThickness,
                X1 = wall.Length,
                Y1 = wall.Width,
                Z1 = wall.Thickness
            });
        }
    }
}
