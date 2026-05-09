//using Microsoft.VisualBasic;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CncWallStation.Plcs.Handlers
//{
//    public static class CableHandler
//    {
//        public static void Handle(CableSlot s, PlcConvertContext ctx)
//        {
//            int f = s.Type switch
//            {
//                CableType.Box => PlcFeatureCode.CableBox,
//                CableType.LType5 => PlcFeatureCode.CableSlotL5,
//                CableType.LType6 => PlcFeatureCode.CableSlotL6,
//                CableType.LType7 => PlcFeatureCode.CableSlotL7,
//                CableType.LType8 => PlcFeatureCode.CableSlotL8,
//                CableType.Free => PlcFeatureCode.FreeSlot,
//                CableType.DiagLU_RD => PlcFeatureCode.DiagLU_RD,
//                CableType.DiagLD_RU => PlcFeatureCode.DiagLD_RU,
//                CableType.DiagWideLD => PlcFeatureCode.DiagWideLD_RU,
//                CableType.DiagWideLU => PlcFeatureCode.DiagWideLU_RD,
//                _ => throw new NotSupportedException()
//            };

//            ctx.Emit(new PlcInstruction
//            {
//                T = PlcTool.SlotCutter,
//                F = f,
//                D = s.Depth,
//                X0 = s.StartX,
//                Y0 = s.StartY,
//                Z0 = 0,
//                X1 = s.EndX,
//                Y1 = s.EndY,
//                Z1 = s.Width     // 斜槽：D=宽度；自由槽：Z1=深度
//            });
//        }
//    }
//}
