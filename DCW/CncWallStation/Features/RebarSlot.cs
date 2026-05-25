using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features
{
    /// <summary>
    /// 钢筋槽方向
    /// </summary>
    public enum RebarSlotDirection
    {
        /// <summary>纵向（沿墙高度方向 Y 轴）</summary>
        Vertical,

        /// <summary>横向（沿墙长度方向 X 轴）</summary>
        Horizontal
    }

    /// <summary>
    /// 钢筋槽（线性切槽，用于植入钢筋）
    ///
    /// 几何描述：
    ///   • LocalPos     — 槽起点（继承自 Feature，墙体局部坐标，俯视图 XY）
    ///   • EndPos       — 槽终点（与 LocalPos 共线）
    ///   • Diameter     — 钢筋直径（决定槽宽，对应 BimRebarDto.Diameter）
    ///   • Depth        — 槽深（继承自 Feature，对应 Horizontal/VerticalDepth）
    ///   • Direction    — 横/纵向
    ///   • StartThreading / EndThreading — 端部是否需要套丝（对应 BimRodDto）
    /// </summary>
    public class RebarSlot : Feature
    {
        // ──────────────────────────────────────────────
        // 几何属性
        // ──────────────────────────────────────────────
        /// <summary>槽起点</summary>
        public Vec2 StartPos => LocalPos;
        /// <summary>槽终点（墙体局部坐标，槽中心线）</summary>
        public Vec2 EndPos { get; set; }

        /// <summary>钢筋直径（mm，决定槽宽）</summary>
        public float Diameter { get; set; }

        /// <summary>槽方向（横 / 纵）</summary>
        public RebarSlotDirection Direction { get; set; }

        // ──────────────────────────────────────────────
        // 钢筋属性（来自 BIM 模型）
        // ──────────────────────────────────────────────

        /// <summary>起点是否套丝</summary>
        public bool StartThreading { get; set; }

        /// <summary>终点是否套丝</summary>
        public bool EndThreading { get; set; }

        /// <summary>钢筋编号（对应 BimRebarDto.Pn）</summary>
        public string? Pn { get; set; }

        // ──────────────────────────────────────────────
        // 计算属性
        // ──────────────────────────────────────────────
        /// <summary>槽长度</summary>
        [JsonPropertyName("length")]
        public float Length => (EndPos - LocalPos).Length();

        /// <summary>槽宽度（默认等于钢筋直径，可由刀径补偿调整）</summary>
        [JsonPropertyName("width")]
        public float Width => Diameter;

        // ──────────────────────────────────────────────
        // 构造
        // ──────────────────────────────────────────────
        public RebarSlot(string id,
                         MachineSide side,
                         Vec2 startPos,
                         Vec2 endPos,
                         float diameter,
                         float depth,
                         RebarSlotDirection direction)
            : base(id, FeatureType.RebarSlot, side, startPos, depth)
        {
            EndPos = endPos;
            Diameter = diameter;
            Direction = direction;
        }

        public RebarSlot() : base() { }

        // ──────────────────────────────────────────────
        // 翻面处理：除了 LocalPos（基类已处理），还需翻 EndPos
        // ──────────────────────────────────────────────

        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            base.ApplyFlip(axis, bounds);
            EndPos = FlipRemapper.RemapPoint(EndPos, axis, bounds);
        }

        // ──────────────────────────────────────────────
        // 信息输出
        // ──────────────────────────────────────────────

        public override string GetInfo() =>
            $"[RebarSlot {Id}] Start={LocalPos}, End={EndPos}, " +
            $"Φ={Diameter}, D={Depth}, L={Length:F1}, Dir={Direction}, " +
            $"Threading=[{StartThreading}/{EndThreading}]" +
            (string.IsNullOrEmpty(Pn) ? "" : $", PN={Pn}");
    }
}