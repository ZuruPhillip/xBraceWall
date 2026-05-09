//using CncWallStation.Features.Grooves;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CncWallStation.Plcs.Handlers
//{
//    public static class RebarSlotHandler
//    {
//        public static void Handle(Groove g, PlcConvertContext ctx)
//        {
//            int f = (g.Direction, g.Continuous) switch
//            {
//                (Dir.Vertical, false) => PlcFeatureCode.RebarVert,       // F6
//                (Dir.Horizontal, false) => PlcFeatureCode.RebarHorz,       // F5
//                (Dir.Vertical, true) => PlcFeatureCode.RebarVertCont,   // F16
//                (Dir.Horizontal, true) => PlcFeatureCode.RebarHorzCont,   // F15
//                _ => throw new NotSupportedException()
//            };

//            ctx.Emit(new PlcInstruction
//            {
//                T = PlcTool.RebarCutter,
//                F = f,
//                D = g.RepeatCount,
//                X0 = g.StartX,
//                Y0 = g.StartY,
//                Z0 = 0,
//                X1 = g.Direction == Dir.Vertical ? g.Pitch : g.Length,
//                Y1 = g.Direction == Dir.Vertical ? g.Length : g.Pitch,
//                Z1 = g.Depth
//            });
//        }
//    }
//}
