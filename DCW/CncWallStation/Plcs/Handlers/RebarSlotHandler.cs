using CncWallStation.Features;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs.Handlers
{
    /// <summary>
    /// 钢筋槽处理器（T2 F15/F16，统一连续槽模式）
    ///
    ///   分组规则：同面 + 同长度 + 同方向 + 同直径 → 合并批量
    ///   全部使用连续指令：F15(横连续)/F16(纵连续)，D=复制次数
    ///
    /// 字段映射：
    ///   T  = 2           （钢筋槽刀）
    ///   F  = 15/16       （横连续 / 纵连续）
    ///   D  = 复制次数（链长-1，单槽时 D=0）
    ///   X0 = 起点 X
    ///   Y0 = 起点 Y
    ///   Z0 = 0
    ///   横槽：X1 = 钢筋长度，Y1 = 间距
    ///   纵槽：X1 = 间距，Y1 = 钢筋长度
    ///   Z1 = 槽深
    /// </summary>
    public static class RebarSlotHandler
    {
        private const float PosTol = 1f;
        private const float LengthTol = 0.5f;
        private const float DiamTol = 0.5f;

        /// <summary>
        /// 批量处理钢筋槽列表
        ///
        ///   1. 按 (面, 长度, 方向, 直径) 分组
        ///   2. 每组内通过 CollinearMerger 查找等间距共线序列
        ///   3. 全部以连续指令发射（F15/F16）
        /// </summary>
        public static void HandleBatch(List<RebarSlot> slots, PlcConvertContext ctx, MomWall wall)
        {
            if (slots == null || slots.Count == 0) return;

            var groups = slots.GroupBy(s => new RebarSlotKey(
                s.Face,
                RoundByTol(s.Length, LengthTol),
                s.Direction,
                RoundByTol(s.Diameter, DiamTol)));

            foreach (var grp in groups)
            {
                var list = grp.ToList();

                foreach (var chain in CollinearMerger.Merge(
                    list,
                    s => (s.StartPos.X, s.StartPos.Y),
                    PosTol))
                {
                    Emit(chain, grp.Key, ctx);
                }
            }
        }

        private static void Emit(
            CollinearMerger.Chain<RebarSlot> chain, RebarSlotKey key, PlcConvertContext ctx)
        {
            var first = chain.First;

            int f = key.Direction switch
            {
                RebarSlotDirection.Horizontal => PlcFeatureCode.RebarHorz, // F5
                RebarSlotDirection.Vertical   => PlcFeatureCode.RebarVert, // F6
                _ => throw new NotSupportedException(key.Direction.ToString())
            };

            (float x1, float y1) = key.Direction switch
            {
                // 横槽：X1 = 钢筋长度，Y1 = 间距
                RebarSlotDirection.Horizontal => (key.Length, chain.Dy),
                // 纵槽：X1 = 间距，Y1 = 钢筋长度
                RebarSlotDirection.Vertical   => (chain.Dx, key.Length),
                _ => throw new NotSupportedException(key.Direction.ToString())
            };

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.RebarCutter,   // T2
                F = f,
                D = chain.CopyCount,
                X0 = first.StartPos.X,
                Y0 = first.StartPos.Y,
                Z0 = 0f,
                X1 = x1,
                Y1 = y1,
                Z1 = first.Depth
            });
        }

        private static float RoundByTol(float v, float tol)
        {
            if (tol <= 0) return v;
            return MathF.Round(v / tol) * tol;
        }

        private readonly record struct RebarSlotKey(
            MachineFace Face,
            float Length,
            RebarSlotDirection Direction,
            float Diameter);
    }
}
