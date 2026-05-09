using CncWallStation.Features;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// BendingKey（吊钩条形孔）处理器
    ///
    ///   形状：Slotted
    ///   面  ：Back / Front
    ///   刀具：T3 F0
    /// </summary>
    public static class BendingKeyHandler
    {
        private const float TOL = 1f;
        private const float DIM_TOL = 0.5f;

        public static void HandleBatch(List<Hole> holes, PlcConvertContext ctx, MomWall wall)
        {
            var slots = holes.Where(h => h.Shape == HoleShape.Slotted).ToList();
            if (slots.Count == 0) return;

            // 1. 校验
            foreach (var h in slots) ValidateBendingKey(h);

            // 2. 分组：同面 + 同长 + 同宽 + 同深 才能合并
            var groups = slots.GroupBy(h => new BendingKeyKey(
                h.Face,
                Round(h.SlotLength),
                Round(h.Radius),
                Round(h.Depth)));

            // 3. 每组通过 CollinearMerger 合并
            foreach (var grp in groups)
            {
                var list = grp.ToList();

                foreach (var chain in CollinearMerger.Merge(
                    list,
                    h => (h.LocalPos.X, h.LocalPos.Y),
                    TOL))
                {
                    EmitInstruction(chain, grp.Key, ctx);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 校验
        // ══════════════════════════════════════════════════════

        private static void ValidateBendingKey(Hole h)
        {
            var side = h.Face.InitialSide;
            if (side != MachineSide.Back && side != MachineSide.Front)
                throw new NotSupportedException(
                    $"BendingKey 仅支持 Back/Front 面，当前: {side}。" +
                    $"位置: {h.LocalPos}, 形状: {h.Shape}");
        }

        // ══════════════════════════════════════════════════════
        // 指令装填
        // ══════════════════════════════════════════════════════

        private static void EmitInstruction(
            CollinearMerger.Chain<Hole> chain, BendingKeyKey key, PlcConvertContext ctx)
        {
            var first = chain.First;

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.LargeDrill,            // T3
                F = PlcFeatureCode.PositionCode,    // F0
                D = chain.CopyCount,
                X0 = first.LocalPos.X,
                Y0 = 0,
                Z0 = first.LocalPos.Y,
                X1 = chain.Dx,
                Y1 = key.Depth,                    // 孔深
                Z1 = 0                // 条孔长
            });
        }

        // ══════════════════════════════════════════════════════
        // 工具
        // ══════════════════════════════════════════════════════

        private static float Round(float v) => MathF.Round(v, 1);

        private readonly record struct BendingKeyKey(
            MachineFace Face, float SlotLength, float SlotWidth, float Depth);
    }
}