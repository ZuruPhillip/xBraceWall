using CncWallStation.Features;

namespace CncWallStation.Plcs.Handlers
{
    public static class WindowHandler
    {
        public static void Handle(Window w, PlcConvertContext ctx)
        {
            float startX = w.LocalPos.X;
            float startY = w.LocalPos.Y;

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.WindowCutter,
                F = PlcFeatureCode.Window,
                D = 5,//默认5层切削
                X0 = startX,
                Y0 = startY,
                Z0 = 0,
                X1 = w.Width,
                Y1 = w.Height,
                Z1 = w.Depth            //ctx.TargetThickness   // 穿透
            });
        }
    }
}
