using CncWallStation.Features;
using Infrastructure.Maths;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Features.MepSlots
{
    /// <summary>
    /// MepSlot 分段器
    ///
    /// 将一条经过原点变换 + 翻面 + NormalizeBottomUp 归一化后的 MepSlot 路径，
    /// 按以下规则切分为若干新的 <see cref="MepSlot"/> 分段集合，供 PLC 数据生成使用：
    ///
    /// ┌───────────────────────────────────────────────────────────────────────┐
    /// │ 1. 直线分段：仅将路径上连续、且宽度(OverrideWidth ?? Width)与深度都相同  │
    /// │    的相邻直线段合并为同一分段；圆弧段(arc)作为强制断点。                 │
    /// │ 2. arc 分段：取上一段直线中点 prevMid 与下一段直线中点 nextMid 作为切割点，│
    /// │    构造一个独立 MepSlot，内容 = [prevMid→prevEnd] + Arc + [nextStart→nextMid]，│
    /// │    起点为 prevMid，终点为 nextMid。                                    │
    /// │ 3. 切割点连续性：prevMid 成为前一直线分段的终点、当前 arc 分段的起点；   │
    /// │    nextMid 成为当前 arc 分段的终点、后一分段的起点，保证首尾严格连续。   │
    /// │ 4. 异常处理：若 arc 上一段或下一段不是直线段，则记录异常到日志，          │
    /// │    且不创建该 arc 对应的分段。                                          │
    /// └───────────────────────────────────────────────────────────────────────┘
    /// </summary>
    public static class MepSlotPartitioner
    {
        private const float Tol = 1e-3f;

        /// <summary>
        /// 将单个 MepSlot 切分为分段集合。
        /// </summary>
        /// <param name="slot">已变换/翻面/归一化的 MepSlot</param>
        /// <param name="logger">可选日志；用于记录 arc 前后非直线的异常</param>
        /// <returns>分段集合，每个元素复用 MepSlot 实例且首尾连续；顺序按路径先后</returns>
        public static List<MepSlot> Partition(MepSlot slot, ILogger? logger = null)
        {
            var result = new List<MepSlot>();
            if (slot == null || slot.Segments.Count == 0)
                return result;

            var segs = slot.Segments;
            int n = segs.Count;

            // 当前直线分段构建器
            var lineBuilder = new LinePartBuilder();

            for (int i = 0; i < n; i++)
            {
                if (segs[i] is LineSegment line)
                {
                    // 计算该直线属于"直线分段"的起止范围（可能被相邻有效 arc 切割到中点）
                    Vec2 start = line.StartPoint;
                    Vec2 end = line.EndPoint;

                    // 前一段是有效 arc：起点部分进入该 arc 分段，直线部分从中点开始
                    if (IsValidArc(segs, i - 1))
                        start = Mid(line.StartPoint, line.EndPoint);

                    // 后一段是有效 arc：终点部分进入该 arc 分段，直线部分到中点结束
                    if (IsValidArc(segs, i + 1))
                        end = Mid(line.StartPoint, line.EndPoint);

                    if ((end - start).Length() > Tol)
                    {
                        float w = slot.GetSegmentWidth(i);
                        AddLinePiece(result, lineBuilder, slot, start, end, w, line.Depth);
                    }
                }
                else if (segs[i] is ArcSegment arc)
                {
                    if (!IsValidArc(segs, i))
                    {
                        // 异常：arc 上一段或下一段不是直线段，记录日志并跳过，不创建该 arc 分段
                        string prev = i > 0 ? segs[i - 1].Type.ToString() : "无";
                        string next = i < n - 1 ? segs[i + 1].Type.ToString() : "无";
                        logger?.LogError(
                            "[MepSlot {MepSlotId}] 圆弧段(index={ArcIndex})上一段或下一段不是直线段，跳过该圆弧分段。上一段={PrevType}，下一段={NextType}",
                            slot.Id, i, prev, next);
                        continue;
                    }

                    // 上一段/下一段均为直线（由 IsValidArc 保证）
                    var prevLine = (LineSegment)segs[i - 1];
                    var nextLine = (LineSegment)segs[i + 1];

                    Vec2 prevMid = Mid(prevLine.StartPoint, prevLine.EndPoint);
                    Vec2 nextMid = Mid(nextLine.StartPoint, nextLine.EndPoint);

                    // 构造 arc 分段：[prevMid→prevEnd] + Arc + [nextStart→nextMid]
                    var arcPart = new MepSlot($"{slot.Id}-arc{i}", slot.InitialSide, slot.Width);

                    arcPart.AddSegment(new LineSegment(prevMid, prevLine.EndPoint, prevLine.Depth)
                    { OverrideWidth = prevLine.OverrideWidth });

                    arcPart.AddArc(arc.Center, arc.Radius,
                                   arc.StartAngleDeg, arc.EndAngleDeg,
                                   arc.Depth, arc.IsClockwise);
                    if (arc.OverrideWidth.HasValue)
                        arcPart.Segments[^1].OverrideWidth = arc.OverrideWidth;

                    arcPart.AddSegment(new LineSegment(nextLine.StartPoint, nextMid, nextLine.Depth)
                    { OverrideWidth = nextLine.OverrideWidth });

                    result.Add(arcPart);
                }
            }

            FlushLinePart(lineBuilder);
            return result;
        }

        // ══════════════════════════════════════════════════
        // 直线分段构建
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 将单个直线片段追加到直线分段（同宽同深且连续则合并，否则开启新分段）。
        /// </summary>
        private static void AddLinePiece(
            List<MepSlot> result,
            LinePartBuilder b,
            MepSlot slot,
            Vec2 start, Vec2 end,
            float width, float depth)
        {
            bool needsNew = b.Part == null
                             || !NearlyEqual(width, b.Width)
                             || !NearlyEqual(depth, b.Depth)
                             || !Connected(b.Part, start);

            if (needsNew)
            {
                FlushLinePart(b);
                b.Part = new MepSlot(
                    $"{slot.Id}-line{result.Count + 1}", slot.InitialSide, width);
                b.Width = width;
                b.Depth = depth;
                result.Add(b.Part);
            }

            if (b.Part!.Segments.Count == 0)
                b.Part.AddLine(start, end, depth);
            else
                b.Part.LineTo(end, depth);

            // 保留段级宽度覆盖（与槽默认宽度不同时）
            if (!NearlyEqual(width, slot.Width))
                b.Part.Segments[^1].OverrideWidth = width;
        }

        /// <summary>重置直线分段构建器状态（arc 强制断点 / 分段完成时调用）</summary>
        private static void FlushLinePart(LinePartBuilder b)
        {
            // 分段对象已在创建时加入 result，此处仅重置构建器状态。
            b.Part = null;
            b.Width = 0f;
            b.Depth = 0f;
        }

        // ══════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════

        /// <summary>直线中点</summary>
        private static Vec2 Mid(Vec2 a, Vec2 b) => (a + b) * 0.5f;

        /// <summary>判断 idx 处是否为"前后均为直线段"的有效圆弧</summary>
        private static bool IsValidArc(List<ISegment> segs, int idx)
            => idx > 0 && idx < segs.Count - 1
               && segs[idx] is ArcSegment
               && segs[idx - 1] is LineSegment
               && segs[idx + 1] is LineSegment;

        /// <summary>新片段起点是否与当前分段终点连续（用于判断是否跨 arc 断点）</summary>
        private static bool Connected(MepSlot part, Vec2 start)
        {
            if (part.Segments.Count == 0) return false;
            return (start - part.Segments[^1].EndPoint).Length() <= Tol;
        }

        /// <summary>浮点近似相等</summary>
        private static bool NearlyEqual(float a, float b)
            => MathF.Abs(a - b) < Tol;

        // ══════════════════════════════════════════════════
        // 直线分段构建器状态
        // ══════════════════════════════════════════════════

        private sealed class LinePartBuilder
        {
            public MepSlot? Part;
            public float Width;
            public float Depth;
        }
    }
}
