using CncWallStation.Features;

namespace CncWallStation.Plcs.Handlers
{
    public static class BoxHandler
    {
        public static void HandleBatch(List<Pocket> boxes, PlcConvertContext ctx)
        {
            if (boxes.Count == 0) return;

            foreach (var box in boxes)
            {
                Handle(box, ctx);
            }
        }

        public static void Handle(Pocket p, PlcConvertContext ctx)
        {
            float startX = p.LocalPos.X - p.Length / 2;
            float startY = p.LocalPos.Y - p.Width / 2;

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter,
                F = PlcFeatureCode.CableBox,
                D = 0,
                X0 = startX,
                Y0 = startY,
                Z0 = 0,
                X1 = p.Length,
                Y1 = p.Width,
                Z1 = p.Depth
            });
        }
    }
}
