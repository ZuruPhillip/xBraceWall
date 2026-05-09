//using CncWallStation.Features;
//using CncWallStation.Features.Props;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CncWallStation.Plcs.Handlers
//{
//    public static class ProppingHandler
//    {
//        public static void Handle(Propping p, PlcConvertContext ctx)
//        {
//            foreach (var cut in p.Cuts)
//            {
//                int f = cut.Side switch
//                {
//                    MachineSide.Top => PlcFeatureCode.StepY_Auto_PosY,   // 顶面 → +Y 补偿
//                    MachineSide.Front => PlcFeatureCode.StepX_Auto_NegX,   // 侧面 → -X 补偿
//                    _ => throw new NotSupportedException()
//                };

//                var bbox = Bounds.Of(cut.Outline);
//                ctx.Emit(new PlcInstruction
//                {
//                    T = PlcTool.StepCutter,
//                    F = f,
//                    D = 0,
//                    X0 = bbox.MinX,
//                    Y0 = bbox.MinY,
//                    Z0 = 0,
//                    X1 = bbox.MaxX,
//                    Y1 = bbox.MaxY,
//                    Z1 = cut.Depth
//                });
//            }
//        }
//    }
//}
