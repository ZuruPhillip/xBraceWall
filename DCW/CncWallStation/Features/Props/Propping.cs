using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features.Props
{
    /// <summary>
    /// 多面组合切割槽（Propping）
    ///
    /// 实际加工方式：
    ///   墙体绕轴旋转后，分别从不同面下刀切割出一个统一深度的多边形，
    ///   多次平面切割叠加 = 立体槽效果。
    ///
    /// 典型用例：
    ///   • 斜撑槽：Top 面切长条 + Front 面切梯形
    ///   • 贯穿槽：Top 面切开口 + Bottom 面切开口
    ///   • L 型槽：Top 面 + 侧面
    ///
    /// ┌───────────────────────────────────────────┐
    /// │  Top View   →  切 Outline₁ (深 d₁)        │
    /// │  Front View →  切 Outline₂ (深 d₂)        │
    /// │  两次切割的布尔组合 = 最终立体形状         │
    /// └───────────────────────────────────────────┘
    /// </summary>
    public class Propping : Feature
    {
        // ══════════════════════════════════════════════════════
        // 属性
        // ══════════════════════════════════════════════════════

        /// <summary>业务分类</summary>
        public PropType PropType { get; set; }

        /// <summary>
        /// 组成本 Propping 的所有切割（通常为 2 个：Top + Front）
        /// 加工顺序 = 列表顺序
        /// </summary>
        public List<PropCut> Cuts { get; set; } = new();

        /// <summary>
        /// 切割组合模式
        /// • Intersection：两次切割相交区域才成为空腔（常用于凸台/斜面）
        /// • Union       ：任一次切割都形成空腔（常用于多位置贯穿）
        /// </summary>
        public CombineMode Combine { get; set; } = CombineMode.Intersection;

        // ══════════════════════════════════════════════════════
        // 派生属性
        // ══════════════════════════════════════════════════════

        [JsonPropertyName("cutCount")]
        public int CutCount => Cuts.Count;

        /// <summary>所有切割中最大的深度（用于包围盒估算）</summary>
        [JsonPropertyName("maxDepth")]
        public float MaxDepth
        {
            get
            {
                float max = 0f;
                foreach (var c in Cuts)
                    if (c.Depth > max) max = c.Depth;
                return max;
            }
        }

        // ══════════════════════════════════════════════════════
        // 构造
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 通用构造：传入多个切割
        /// </summary>
        public Propping(
            string id,
            List<PropCut> cuts,
            PropType propType = PropType.General,
            CombineMode combine = CombineMode.Intersection)
            : base(id, FeatureType.Propping,
                   side: cuts.Count > 0 ? cuts[0].Side : MachineSide.Top,
                   localPos: cuts.Count > 0 ? ComputeCentroid(cuts[0].Outline) : Vec2.Zero,
                   depth: cuts.Count > 0 ? cuts[0].Depth : 0f)
        {
            if (cuts == null || cuts.Count == 0)
                throw new ArgumentException("至少需要一次切割", nameof(cuts));

            Cuts = new List<PropCut>(cuts);
            PropType = propType;
            Combine = combine;
        }

        /// <summary>
        /// 双面切割构造（最常见）
        /// </summary>
        public Propping(
            string id,
            PropCut primaryCut,
            PropCut secondaryCut,
            PropType propType = PropType.General,
            CombineMode combine = CombineMode.Intersection)
            : this(id,
                   new List<PropCut> { primaryCut, secondaryCut },
                   propType,
                   combine)
        { }

        public Propping() : base() { }
        // ══════════════════════════════════════════════════════
        // 工厂方法
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 构造斜撑槽：Top 面切长条 + Front 面切梯形
        /// </summary>
        /// <param name="topOutline">俯视多边形（XY）</param>
        /// <param name="topDepth">俯视切割深度</param>
        /// <param name="frontOutline">正视多边形（XZ）</param>
        /// <param name="frontDepth">正视切割深度</param>
        public static Propping CreateDualFace(
            string id,
            List<Vec2> topOutline, float topDepth,
            List<Vec2> frontOutline, float frontDepth,
            PropType propType = PropType.General)   
        {
            var topCut = new PropCut(MachineSide.Top, topOutline, topDepth);
            var frontCut = new PropCut(MachineSide.Front, frontOutline, frontDepth);

            return new Propping(id, topCut, frontCut, propType,
                                CombineMode.Intersection);
        }

        /// <summary>
        /// 构造贯穿槽：Top + Bottom 两面对切
        /// </summary>
        public static Propping CreateThroughSlot(
            string id,
            List<Vec2> outline,
            float halfThickness,
            PropType propType = PropType.General)
        {
            var topCut = new PropCut(MachineSide.Top, outline, halfThickness);
            var bottomCut = new PropCut(MachineSide.Bottom, outline, halfThickness);

            return new Propping(id, topCut, bottomCut, propType,
                                CombineMode.Union);
        }

        // ══════════════════════════════════════════════════════
        // 翻面
        // ══════════════════════════════════════════════════════

        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            // 每个切割的多边形坐标都要重映射
            foreach (var cut in Cuts)
            {
                for (int i = 0; i < cut.Outline.Count; i++)
                    cut.Outline[i] = FlipRemapper.RemapPoint(cut.Outline[i], axis, bounds);

                // 加工面也要随之翻转
                cut.Side = FlipRemapper.RemapSide(cut.Side, axis);
            }

            if (Cuts.Count > 0)
                LocalPos = ComputeCentroid(Cuts[0].Outline);

            Face.ApplyFlipSide(
                FlipRemapper.RemapSide(Face.GetCurrentSide(), axis));
        }

        // ══════════════════════════════════════════════════════
        // 工具
        // ══════════════════════════════════════════════════════

        private static Vec2 ComputeCentroid(List<Vec2> poly)
        {
            if (poly == null || poly.Count == 0) return Vec2.Zero;
            float sx = 0, sy = 0;
            foreach (var p in poly) { sx += p.X; sy += p.Y; }
            return new Vec2(sx / poly.Count, sy / poly.Count);
        }

        // ══════════════════════════════════════════════════════
        // 信息输出
        // ══════════════════════════════════════════════════════

        public override string GetInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Propping {Id}] Type={PropType,-10} Mode={Combine,-12} " +
                      $"Cuts={CutCount}  MaxD={MaxDepth:F2}mm\n");

            for (int i = 0; i < Cuts.Count; i++)
                sb.Append($"    Cut#{i}: {Cuts[i]}\n");

            return sb.ToString().TrimEnd();
        }
    }

    // ══════════════════════════════════════════════════════════
    // 辅助枚举
    // ══════════════════════════════════════════════════════════

    public enum PropType
    {
        General,
        Custom
    }

    public enum CombineMode
    {
        /// <summary>两次切割的交集（只有重叠区域才被挖空）</summary>
        Intersection,

        /// <summary>两次切割的并集（任一切割都形成空腔）</summary>
        Union
    }
}
