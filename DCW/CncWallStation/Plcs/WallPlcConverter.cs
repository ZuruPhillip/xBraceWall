using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.Features.Props;
using CncWallStation.MomWallData;
using CncWallStation.Plcs.Handlers;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Plcs
{
    public static class WallPlcConverter
    {
        /// <summary>
        /// 按 Handler 分组生成 PLC 指令，返回分组结果（全部特征，D=1）
        /// </summary>
        public static List<PlcFeatureGroup> ConvertGrouped(MomWall wall)
        {
            return ConvertGrouped(wall, wall.Features, 1);
        }

        /// <summary>
        /// 按 Handler 分组生成 PLC 指令，返回分组结果
        /// 使用指定的特征列表和墙定义 D 值
        /// </summary>
        /// <param name="wall">墙体数据（提供尺寸信息）</param>
        /// <param name="features">参与转换的特征列表（已按正反面筛选）</param>
        /// <param name="wallDefineD">墙定义 D 值（正面=1，反面=5）</param>
        /// <param name="logger">可选日志；用于电缆槽分段异常记录</param>
        public static List<PlcFeatureGroup> ConvertGrouped(
            MomWall wall, List<Feature> features, int wallDefineD,
            ILogger? logger = null)
        {
            var ctx = new PlcConvertContext();
            var groups = new List<PlcFeatureGroup>();

            // ========== 1. 墙定义 ==========
            int before = ctx.Output.Count;
            WallHandler.Handle(wall, ctx, wallDefineD);
            AddGroupIfNotEmpty(groups, "WallHandler", "墙定义", ctx.Output, before);

            // 收集 Features 按类型分发
            var holes = new List<Hole>();
            var grooves = new List<Groove>();
            var rebarSlots = new List<RebarSlot>();
            var cableSlots = new List<MepSlot>();
            var boxes = new List<Pocket>();

            foreach (var f in features)
            {
                switch (f)
                {
                    case Groove g: grooves.Add(g); break;
                    case Hole h: holes.Add(h); break;
                    case Pocket p:
                        boxes.Add(p);
                        break;
                    case RebarSlot r: rebarSlots.Add(r); break;
                    case Window w:
                        before = ctx.Output.Count;
                        WindowHandler.Handle(w, ctx);
                        AddGroupIfNotEmpty(groups, "WindowHandler", "窗户", ctx.Output, before);
                        break;
                    case MepSlot m: cableSlots.Add(m); break;
                    case Propping pr:
                        before = ctx.Output.Count;
                        ProppingHandler.Handle(pr, ctx);
                        AddGroupIfNotEmpty(groups, "ProppingHandler", "斜撑", ctx.Output, before);
                        break;
                    default: throw new NotSupportedException(f.GetType().Name);
                }
            }

            // ========== 批量处理 ==========

            // 普通孔（圆形孔）
            var circleHoles = holes.Where(h => h.Shape == HoleShape.Round).ToList();
            if (circleHoles.Count > 0)
            {
                before = ctx.Output.Count;
                HoleHandler.HandleBatch(circleHoles, ctx, wall);
                AddGroupIfNotEmpty(groups, "HoleHandler", "普通孔", ctx.Output, before);
            }

            // 定位孔（条形孔）
            var slotHoles = holes.Where(h => h.Shape == HoleShape.Slotted).ToList();
            if (slotHoles.Count > 0)
            {
                before = ctx.Output.Count;
                BendingKeyHandler.HandleBatch(slotHoles, ctx, wall);
                AddGroupIfNotEmpty(groups, "BendingKeyHandler", "定位孔", ctx.Output, before);
            }

            // 钢筋槽
            if (rebarSlots.Count > 0)
            {
                before = ctx.Output.Count;
                RebarSlotHandler.HandleBatch(rebarSlots, ctx, wall);
                AddGroupIfNotEmpty(groups, "RebarSlotHandler", "钢筋槽", ctx.Output, before);
            }

            // 电缆槽
            if (cableSlots.Count > 0)
            {
                before = ctx.Output.Count;

                // 对每个 MepSlot 进行分段处理，生成分段集合后批量发射指令
                var cablePartitions = new List<MepSlot>();
                foreach (var slot in cableSlots)
                    cablePartitions.AddRange(MepSlotPartitioner.Partition(slot, logger));

                CableHandler.HandleBatch(cablePartitions, ctx);
                AddGroupIfNotEmpty(groups, "CableHandler", "电缆槽", ctx.Output, before);
            }

            // 台阶
            var stepGrooves = grooves.Where(
                g => g.GrooveType == GrooveType.SteelColumn
                || g.GrooveType == GrooveType.BaseBracket
                || g.GrooveType == GrooveType.TopBracket)
                .ToList();
            if (stepGrooves.Count > 0)
            {
                before = ctx.Output.Count;
                foreach (var step in stepGrooves)
                {
                    StepHandler.Handle(step, ctx, wall);
                }
                AddGroupIfNotEmpty(groups, "StepHandler", "钢柱槽", ctx.Output, before);
            }

            // 密封条
            var glueSealGrooves = grooves.Where(g => g.GrooveType == GrooveType.GlueSeal).ToList();
            foreach (var glueSeal in glueSealGrooves)
            {
                before = ctx.Output.Count;
                GlueSealHandler.Handle(glueSeal, ctx, wall);
                AddGroupIfNotEmpty(groups, "GlueSealHandler", "密封条", ctx.Output, before);
            }

            // 顶板
            var topPlateGrooves = grooves.Where(g => g.GrooveType == GrooveType.TopPlate).ToList();
            foreach (var topPlate in topPlateGrooves)
            {
                before = ctx.Output.Count;
                TopPlateHandler.Handle(topPlate, ctx, wall);
                AddGroupIfNotEmpty(groups, "TopPlateHandler", "顶板", ctx.Output, before);
            }

            // X 斜槽
            var xBraceGrooves = grooves.Where(g => g.GrooveType == GrooveType.XBraceSteel).ToList();
            if (xBraceGrooves.Count > 0)
            {
                before = ctx.Output.Count;
                foreach (var xBrace in xBraceGrooves)
                {
                    XBraceHandler.Handle(xBrace, ctx, wall);
                }
                AddGroupIfNotEmpty(groups, "XBraceHandler", "X斜槽", ctx.Output, before);
            }

            //开关盒
            if (boxes.Count > 0)
            {
                before = ctx.Output.Count;
                BoxHandler.HandleBatch(boxes, ctx);
                AddGroupIfNotEmpty(groups, "BoxHandler", "开关盒", ctx.Output, before);
            }
            return groups;
        }

        /// <summary>
        /// 若 before → after 之间有新指令，则添加一个分组
        /// </summary>
        private static void AddGroupIfNotEmpty(
            List<PlcFeatureGroup> groups,
            string handlerName,
            string featureName,
            List<PlcInstruction> allInstructions,
            int before)
        {
            int after = allInstructions.Count;
            if (after > before)
            {
                var instructions = allInstructions.Skip(before).Take(after - before).ToList();
                groups.Add(new PlcFeatureGroup
                {
                    HandlerName = handlerName,
                    FeatureName = featureName,
                    Instructions = instructions
                });
            }
        }
    }
}
