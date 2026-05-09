//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CncWallStation.Plcs.Handlers
//{
//    public static class WindowHandler
//    {
//        public static void Handle(Window w, PlcConvertContext ctx)
//        {
//            ctx.Emit(new PlcInstruction
//            {
//                T = PlcTool.WindowCutter,
//                F = PlcFeatureCode.Window,
//                D = w.LayerCount,               // 例 5 层切削
//                X0 = w.X,
//                Y0 = w.Y,
//                Z0 = 0,
//                X1 = w.Width,
//                Y1 = w.Height,
//                Z1 = ctx.TargetThickness   // 穿透
//            });
//        }
//    }
//}
