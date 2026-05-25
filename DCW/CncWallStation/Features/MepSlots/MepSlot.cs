using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features.MepSlots
{
    /// <summary>
    /// 电线管道线槽特征
    /// ┌──────────────────────────────────────────────────┐
    /// │  路径 = 直线段 + 圆弧段混合列表                    │
    /// │  宽度全槽统一，深度各段独立                         │
    /// │  支持翻面坐标重映射 / 世界坐标输出                  │
    /// └──────────────────────────────────────────────────┘
    /// </summary>
    public class MepSlot : Feature
    {
        // ══════════════════════════════════════════════════
        // 属性
        // ══════════════════════════════════════════════════

        /// <summary>线槽宽度（mm，全槽统一）</summary>
        public float Width { get; set; }

        /// <summary>路径段列表（有序，首尾相连）</summary>
        public List<ISegment> Segments { get; private set; }
            = new List<ISegment>();

        // ── 派生属性 ──────────────────────────────────────

        /// <summary>段数</summary>
        public int SegmentCount => Segments.Count;

        /// <summary>路径总长度</summary>
        public float TotalLength => Segments.Sum(s => s.Length);

        /// <summary>最小深度</summary>
        [JsonIgnore]
        public float MinDepth => Segments.Count > 0
                                      ? Segments.Min(s => s.Depth) : 0f;

        /// <summary>最大深度</summary>
        [JsonIgnore]
        public float MaxDepth => Segments.Count > 0
                                      ? Segments.Max(s => s.Depth) : 0f;

        /// <summary>是否深度均一</summary>
        public bool IsUniformDepth
            => Segments.All(s => MathF.Abs(s.Depth - Segments[0].Depth) < 0.001f);

        /// <summary>路径起点</summary>
        public Vec2? PathStart => Segments.Count > 0
                                      ? Segments[0].StartPoint : null;

        /// <summary>路径终点</summary>
        public Vec2? PathEnd => Segments.Count > 0
                                      ? Segments[^1].EndPoint : null;

        // ══════════════════════════════════════════════════
        // 构造
        // ══════════════════════════════════════════════════

        public MepSlot(string id, MachineSide side, float width)
            : base(id, FeatureType.MepSlot, side,
                   Vec2.Zero,   // LocalPos 由第一段起点动态决定
                   0f)          // Depth 由各段独立管理
        {
            Width = width;
        }

        public MepSlot(string id, Vec3 customNormal, float width)
            : base(id, FeatureType.MepSlot, customNormal,
                   Vec2.Zero, 0f)
        {
            Width = width;
        }

        public MepSlot() : base() { }
        // ══════════════════════════════════════════════════
        // 段管理（链式调用）
        // ══════════════════════════════════════════════════

        /// <summary>追加直线段</summary>
        public MepSlot AddLine(Vec2 start, Vec2 end, float depth)
        {
            ValidateContinuity(start);
            Segments.Add(new LineSegment(start, end, depth));
            SyncLocalPos();
            return this;
        }

        /// <summary>追加直线段（自动从上一段终点出发）</summary>
        public MepSlot LineTo(Vec2 end, float depth)
        {
            var start = RequireLastEnd();
            Segments.Add(new LineSegment(start, end, depth));
            return this;
        }

        /// <summary>追加圆弧段（圆心 + 角度定义）</summary>
        public MepSlot AddArc(Vec2 center,
                              float radius,
                              float startAngleDeg,
                              float endAngleDeg,
                              float depth,
                              bool isClockwise = false)
        {
            var seg = new ArcSegment(center, radius,
                                     startAngleDeg, endAngleDeg,
                                     depth, isClockwise);
            ValidateContinuity(seg.StartPoint);
            Segments.Add(seg);
            SyncLocalPos();
            return this;
        }

        /// <summary>追加圆弧段（三点定义）</summary>
        public MepSlot AddArcByThreePoints(
            Vec2 p1, Vec2 pMid, Vec2 p3, float depth)
        {
            ValidateContinuity(p1);
            Segments.Add(ArcSegment.FromThreePoints(p1, pMid, p3, depth));
            SyncLocalPos();
            return this;
        }

        /// <summary>从上一段终点以切线方向追加圆弧</summary>
        public MepSlot ArcTangentTo(Vec2 tangentDir, Vec2 end, float depth)
        {
            var start = RequireLastEnd();
            Segments.Add(ArcSegment.FromTangent(start, tangentDir, end, depth));
            return this;
        }

        /// <summary>追加任意已构造的段</summary>
        public MepSlot AddSegment(ISegment seg)
        {
            ValidateContinuity(seg.StartPoint);
            Segments.Add(seg);
            SyncLocalPos();
            return this;
        }

        /// <summary>修改指定段的深度</summary>
        public MepSlot SetDepth(int segIndex, float depth)
        {
            if (segIndex < 0 || segIndex >= Segments.Count)
                throw new ArgumentOutOfRangeException(nameof(segIndex));
            Segments[segIndex].Depth = depth;
            return this;
        }

        /// <summary>统一设置所有段深度</summary>
        public MepSlot SetUniformDepth(float depth)
        {
            foreach (var s in Segments) s.Depth = depth;
            return this;
        }

        /// <summary>获取某段的实际加工宽度（优先用段自身覆盖值）</summary>
        public float GetSegmentWidth(int segIndex)
        {
            if (segIndex < 0 || segIndex >= Segments.Count)
                throw new ArgumentOutOfRangeException(nameof(segIndex));
            return Segments[segIndex].OverrideWidth ?? Width;
        }

        // ══════════════════════════════════════════════════
        // 路径查询
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 按类型过滤段
        /// </summary>
        public IEnumerable<ISegment> GetSegments(SegmentType type)
            => Segments.Where(s => s.Type == type);

        /// <summary>
        /// 获取全部路径离散点（用于可视化 / 碰撞检测）
        /// </summary>
        public Vec2[] Tessellate(int arcSteps = 16)
        {
            var pts = new List<Vec2>();
            for (int i = 0; i < Segments.Count; i++)
            {
                var seg = Segments[i];
                var piece = seg.Tessellate(arcSteps);

                // 相邻段重复点去重
                if (pts.Count > 0 && piece.Length > 0)
                    pts.AddRange(piece.Skip(1));
                else
                    pts.AddRange(piece);
            }
            return pts.ToArray();
        }

        /// <summary>
        /// 获取包围盒（路径轮廓）
        /// </summary>
        public (Vec2 Min, Vec2 Max) GetPathBounds()
        {
            var pts = Tessellate(32);
            if (pts.Length == 0) return (Vec2.Zero, Vec2.Zero);

            float minX = pts[0].X, minY = pts[0].Y;
            float maxX = pts[0].X, maxY = pts[0].Y;

            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            // 向外扩展半槽宽
            float hw = Width * 0.5f;
            return (new Vec2(minX - hw, minY - hw),
                    new Vec2(maxX + hw, maxY + hw));
        }

        /// <summary>
        /// 检验路径连续性（各段首尾是否相接）
        /// </summary>
        public bool ValidatePath(float tolerance = 0.01f)
        {
            for (int i = 1; i < Segments.Count; i++)
            {
                Vec2 prev = Segments[i - 1].EndPoint;
                Vec2 curr = Segments[i].StartPoint;
                float dist = (curr - prev).Length();
                if (dist > tolerance) return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════
        // 翻面重映射（重写基类）
        // ══════════════════════════════════════════════════

        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            // 重映射所有段
            for (int i = 0; i < Segments.Count; i++)
                Segments[i] = Segments[i].Remap(axis, bounds);

            // 更新加工面
            Face.ApplyFlipSide(FlipRemapper.RemapSide(
                Face.GetCurrentSide(), axis));

            // 同步 LocalPos（始终指向路径起点）
            SyncLocalPos();
        }

        // ══════════════════════════════════════════════════
        // Feature 抽象实现
        // ══════════════════════════════════════════════════

        public override string GetInfo()
        {
            string depthStr = IsUniformDepth
                ? $"{MinDepth:F2}mm"
                : $"{MinDepth:F2}~{MaxDepth:F2}mm";

            return $"[MepSlot {Id}] Side={CurrentSide,-8} " +
                   $"W={Width:F2}mm  Depth={depthStr}  " +
                   $"Segs={SegmentCount}  TotalL={TotalLength:F2}mm  " +
                   $"Start={PathStart}  End={PathEnd}";
        }

        /// <summary>打印所有段详情</summary>
        public void PrintSegments()
        {
            Console.WriteLine($"  ┌─ MepSlot [{Id}]  " +
                              $"W={Width}mm  Segs={SegmentCount}  " +
                              $"TotalL={TotalLength:F2}mm");
            for (int i = 0; i < Segments.Count; i++)
                Console.WriteLine($"  │  [{i}] {Segments[i].GetInfo()}");
            Console.WriteLine($"  └─ 路径连续性: " +
                              $"{(ValidatePath() ? "✓ 正常" : "✗ 断开")}");
        }

        // ══════════════════════════════════════════════════
        // 私有辅助
        // ══════════════════════════════════════════════════

        /// <summary>LocalPos 始终同步为路径第一段起点</summary>
        private void SyncLocalPos()
        {
            if (Segments.Count > 0)
                LocalPos = Segments[0].StartPoint;
        }

        /// <summary>检查新段起点是否与上一段终点连续</summary>
        private void ValidateContinuity(Vec2 newStart, float tolerance = 0.1f)
        {
            if (Segments.Count == 0) return;
            Vec2 lastEnd = Segments[^1].EndPoint;
            float dist = (newStart - lastEnd).Length();
            if (dist > tolerance)
                throw new InvalidOperationException(
                    $"段不连续：上一段终点={lastEnd}，" +
                    $"新段起点={newStart}，距离={dist:F3}mm > {tolerance}mm");
        }

        /// <summary>获取上一段终点，段列表为空时抛出</summary>
        private Vec2 RequireLastEnd()
        {
            if (Segments.Count == 0)
                throw new InvalidOperationException(
                    "路径为空，LineTo/ArcTangentTo 需要先有一个初始段");
            return Segments[^1].EndPoint;
        }
    }
}
