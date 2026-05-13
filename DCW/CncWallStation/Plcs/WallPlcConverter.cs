using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.MomWallData;
using CncWallStation.Plcs.Handlers;

namespace CncWallStation.Plcs
{
    public static class WallPlcConverter
    {
        public static List<PlcInstruction> Convert(MomWall wall, PlcConvertContext ctx)
        {
            WallHandler.Handle(wall, ctx);

            // 2. XPS 偏移（若存在，紧跟墙定义）
            //if (ctx.HasXps) EmitXpsOffset(ctx);

            // 3. 收集所有 Hole，统一批量处理（合并复制）
            var holes = new List<Hole>();
            var grooves = new List<Groove>();
            var rebarSlots = new List<RebarSlot>();
            var cableSlots = new List<MepSlot>();

            // 4. 遍历 Features，按类型分发
            foreach (var f in wall.Features)
            {
                switch (f)
                {
                    case Groove g:grooves.Add(g);break;
                    case Hole h: holes.Add(h); break;
                    case Pocket p: BoxHandler.Handle(p, ctx); break;
                    case RebarSlot r: rebarSlots.Add(r); break;
                    case Window w: WindowHandler.Handle(w, ctx); break;
                    case MepSlot m: cableSlots.Add(m); break;
                    //case Propping pr: ProppingHandler.Handle(pr, ctx); break;
                    default: throw new NotSupportedException(f.GetType().Name);
                }
            }

            // 5. 批量处理 Hole & BendingKey（自动合并等间距共线孔）
            var circleHoles = holes.Where(h => h.Shape == HoleShape.Round).ToList();
            var slotHoles = holes.Where(h => h.Shape == HoleShape.Slotted).ToList();
            if (circleHoles.Count > 0)
                HoleHandler.HandleBatch(circleHoles, ctx, wall);

            if (slotHoles.Count > 0)
                BendingKeyHandler.HandleBatch(slotHoles, ctx, wall);

            //TODO : 仅仅处理当前面
            //if (rebarSlots.Count > 0)
                //RebarSlotHandler.HandleBatch(rebarSlots, ctx, wall);

            //6.批量处理 Step（自动合并等间距共线孔）
            var stepGrooves = grooves.Where(
                g => g.GrooveType == GrooveType.SteelColumn
                || g.GrooveType == GrooveType.BaseBracket
                || g.GrooveType == GrooveType.TopBracket)
                .ToList();
            foreach ( var step in stepGrooves )
            {
                StepHandler.Handle(step, ctx, wall);
            }//
            
            var glueSealGrooves = grooves.Where(g => g.GrooveType == GrooveType.GlueSeal).ToList();
            foreach (var glueSeal in glueSealGrooves)
            {
                GlueSealHandler.Handle(glueSeal, ctx, wall);
            }

            var xBraceGrooves = grooves.Where(g => g.GrooveType == GrooveType.XBraceSteel).ToList();
            foreach (var xBrace in xBraceGrooves)
            {
                XBraceHandler.Handle(xBrace, ctx, wall);
            }

            return ctx.Output;
        }

        

        // ────────── XPS 偏移 ──────────
        private static void EmitXpsOffset(PlcConvertContext ctx)
        {
            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.XpsOffset,
                F = PlcFeatureCode.XpsOffset,
                D = 0,
                X0 = ctx.XpsLeftOffset,
                Y0 = ctx.XpsYExpand,
                Z0 = ctx.XpsZOverCut,
                X1 = ctx.XpsLeftOffset + ctx.XpsRightOffset + ctx.TargetLength - ctx.TargetLength,
                Y1 = ctx.XpsYExpand,
                Z1 = 0
            });
        }
    }
}
