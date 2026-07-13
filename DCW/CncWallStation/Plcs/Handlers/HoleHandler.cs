using CncWallStation.Features;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs.Handlers
{
    public static class HoleHandler
    {
        // ══════════════════════════════════════════════════════
        // 常量
        // ══════════════════════════════════════════════════════

        /// <summary>位置容差（mm）：两孔位置视为对齐的最大差异</summary>
        private const float TOL = 1f;

        /// <summary>孔径匹配容差（mm）</summary>
        private const float DIAMETER_TOL = 0.5f;

        // 仅支持的 4 种孔径
        private const float DIAMETER_25 = 25f;
        private const float DIAMETER_20 = 20f;
        private const float DIAMETER_12 = 12f;

        /// <summary>
        /// 批量处理 Hole 列表
        ///
        /// 仅支持以下 4 种圆孔（其它一律抛 NotSupportedException）：
        ///
        /// 流程：
        ///   1. 仅处理 Round 类型孔
        ///   2. 校验 (孔径, 面) 组合是否合法 → 不合法则抛异常
        ///   3. 按 (面, 孔径, 深度) 分组
        ///   4. 每组内查找等间距共线序列 → 合并为带 D 的复制指令
        ///   5. 剩余孤立孔 → 单条指令（D=0）
        /// </summary>
        public static void HandleBatch(List<Hole> holes, PlcConvertContext ctx, MomWall wall)
        {
            // 1. 仅处理圆孔
            var rounds = holes.Where(h => h.Shape == HoleShape.Round).ToList();
            if (rounds.Count == 0) return;

            // 2. 预先校验 — 任意一个不合法孔都直接抛错
            foreach (var h in rounds)
                ValidateHole(h);

            // 3. 按 (面 + 孔径 + 深度) 分组
            var groups = rounds.GroupBy(h => new HoleKey(
                h.Face.InitialSide,
                Round(h.Diameter),
                Round(h.Depth)
            ));

            // 4. 每组通过 CollinearMerger 合并
            foreach (var grp in groups)
            {
                var list = grp.OrderBy(x => x.LocalPos.X).ToList();

                foreach (var chain in CollinearMerger.Merge(
                    list,
                    h => (h.LocalPos.X, h.LocalPos.Y),
                    TOL))
                {
                    EmitInstruction(chain, grp.Key, ctx, wall.Thickness);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 校验
        // ══════════════════════════════════════════════════════

        /// <summary>校验孔的 (孔径, 面) 组合是否在支持列表内</summary>
        private static void ValidateHole(Hole h)
        {
            float d = h.Diameter;
            MachineSide side = h.Face.InitialSide;

            bool ok =
                // ① 侧面 Φ25 → T3
                (side == MachineSide.Front && Match(d, DIAMETER_25)) ||
                // ② 侧面 Φ12 → T9
                (side == MachineSide.Front && Match(d, DIAMETER_12)) ||
                // ① 侧面 Φ25 → T3
                (side == MachineSide.Back && Match(d, DIAMETER_25)) ||
                // ② 侧面 Φ12 → T9
                (side == MachineSide.Back && Match(d, DIAMETER_12)) ||
                // ③ 顶面 Φ20 → T8
                (side == MachineSide.Top && Match(d, DIAMETER_20)) ||
                // ④ 顶面 Φ12 → T10
                (side == MachineSide.Top && Match(d, DIAMETER_12));

            if (!ok)
            {
                throw new NotSupportedException(
                    $"不支持的圆孔规格: 孔径=Φ{d}, 面={side}。" +
                    $"仅支持: 侧面Φ25/Φ12, 顶面Φ20/Φ12。");
            }
        }

        // ══════════════════════════════════════════════════════
        // 指令装填
        // ══════════════════════════════════════════════════════

        private static void EmitInstruction(
            CollinearMerger.Chain<Hole> chain, HoleKey key, PlcConvertContext ctx, float wallThickness)
        {
            var first = chain.First;

            // 1. 按 (孔径, 面) 选择 T 值与 F 值
            var (t, f) = SelectToolAndFeature(key.Diameter, first.Face.InitialSide);

            // 2. 按面计算 Z0
            //    Bottom（侧面/底面进刀）：Z0 = LocalPos.Z（孔的高度坐标）
            //    Top   （墙正面进刀）   ：Z0 = 0（从墙正面切入）
            float z0 = first.Face.InitialSide switch
            {
                MachineSide.Front => first.LocalPos.Y,
                MachineSide.Back => wallThickness - first.LocalPos.Y,
                MachineSide.Top => 0f,
                _ => throw new NotSupportedException(
                         $"Unsupported face: {first.Face.InitialSide}")
            };

            ctx.Emit(new PlcInstruction
            {
                T = t,
                F = f,
                D = chain.CopyCount,
                X0 = first.LocalPos.X,
                Y0 = 0,
                Z0 = z0,
                X1 = chain.Dx,
                Y1 = key.Depth,
                Z1 = chain.Dy
            });
        }

        /// <summary>
        /// (孔径, 面) → (T 值, F 值) 的精确映射
        ///
        ///   侧面 Φ25 → T3 F2
        ///   侧面 Φ12 → T9 F2
        ///   顶面 Φ20 → T8 F9
        ///   顶面 Φ12 → T10 F9
        /// </summary>
        private static (int t, int f) SelectToolAndFeature(
            float diameter, MachineSide side)
        {
            if (side == MachineSide.Front)
            {
                if (Match(diameter, DIAMETER_25))
                    return (PlcTool.LargeDrill, PlcFeatureCode.BottomHole);   // T3 F9
                if (Match(diameter, DIAMETER_12))
                    return (PlcTool.SmallDrill, PlcFeatureCode.BottomHole);   // T9 F9
            }
            else if (side == MachineSide.Back)
            {
                if (Match(diameter, DIAMETER_20))
                    return (PlcTool.SlotCutter, PlcFeatureCode.FaceHoleCode);  // T8 F9
                if (Match(diameter, DIAMETER_12))
                    return (PlcTool.FaceHole, PlcFeatureCode.FaceHoleCode);  // T10 F9
            }
            else if (side == MachineSide.Top)
            {
                if (Match(diameter, DIAMETER_20))
                    return (PlcTool.SlotCutter, PlcFeatureCode.FaceHoleCode);  // T8 F9
                if (Match(diameter, DIAMETER_12))
                    return (PlcTool.FaceHole, PlcFeatureCode.FaceHoleCode);  // T10 F9
            }

            // 理论上不会到达（已被 ValidateHole 拦截）
            throw new NotSupportedException(
                $"不支持的圆孔规格: Φ{diameter} @ {side}");
        }

        // ══════════════════════════════════════════════════════
        // 工具
        // ══════════════════════════════════════════════════════

        private static float Round(float v) => MathF.Round(v, 1);

        private static bool Match(float a, float b) =>
            MathF.Abs(a - b) <= DIAMETER_TOL;

        // ══════════════════════════════════════════════════════
        // 内部数据
        // ══════════════════════════════════════════════════════

        private readonly record struct HoleKey(
            MachineSide Face, float Diameter, float Depth);
    }
}

