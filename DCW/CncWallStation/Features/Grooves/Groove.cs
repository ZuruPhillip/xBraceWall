using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features.Grooves
{
    /// <summary>
    /// 切槽特征（直线型刀路）
    /// </summary>
    public class Groove : Feature
    {
        // ══════════════════════════════════════════════════════
        // 属性
        // ══════════════════════════════════════════════════════

        /// <summary>槽类型（业务分类）</summary>
        public GrooveType GrooveType { get; set; }

        /// <summary>槽起点（局部坐标，中心线）</summary>
        public Vec2 StartPt { get;  set; }

        /// <summary>槽终点（局部坐标，中心线）</summary>
        public Vec2 EndPt { get;  set; }

        /// <summary>
        /// 中心线左侧宽度（mm）
        /// 定义：沿 StartPt→EndPt 方向，左手侧的宽度
        /// </summary>
        [JsonIgnore]
        public float LeftWidth { get; set; }

        /// <summary>
        /// 中心线右侧宽度（mm）
        /// 定义：沿 StartPt→EndPt 方向，右手侧的宽度
        /// </summary>
        [JsonIgnore]
        public float RightWidth { get; set; }

        /// <summary>中心线长度（mm）</summary>
        [JsonPropertyName("length")]
        public float Length =>
            (EndPt - StartPt).Length();


        /// <summary>总槽宽（只读）= LeftWidth + RightWidth</summary>
        [JsonPropertyName("width")]
        public float Width => LeftWidth + RightWidth;

        /// <summary>是否对称槽（左右宽相等）</summary>
        [JsonPropertyName("isSymmetric")]
        public bool IsSymmetric =>
            MathF.Abs(LeftWidth - RightWidth) < 0.001f;

        /// <summary>中心线方向（单位向量）</summary>
        [JsonIgnore]
        public Vec2 Direction =>
            (EndPt - StartPt).Normalize();

        // ══════════════════════════════════════════════════════
        // 构造 —— 统一宽度（向后兼容）
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 对称槽（左右宽相等），与旧接口兼容
        /// </summary>
        public Groove(string id,
                      MachineSide side,
                      Vec2 startPt,
                      Vec2 endPt,
                      float width,
                      float depth,
                      GrooveType grooveType = GrooveType.General)
            : base(id, FeatureType.Groove, side, startPt, depth)
        {
            StartPt = startPt;
            EndPt = endPt;
            LeftWidth = width * 0.5f;
            RightWidth = width * 0.5f;
            GrooveType = grooveType;
        }

        // ══════════════════════════════════════════════════════
        // 构造 —— 非对称宽度
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 非对称槽（左右宽可不同）
        /// </summary>
        public Groove(string id,
                      MachineSide side,
                      Vec2 startPt,
                      Vec2 endPt,
                      float leftWidth,
                      float rightWidth,
                      float depth,
                      GrooveType grooveType = GrooveType.General)
            : base(id, FeatureType.Groove, side, startPt, depth)
        {
            StartPt = startPt;
            EndPt = endPt;
            LeftWidth = leftWidth;
            RightWidth = rightWidth;
            GrooveType = grooveType;
        }

        // ══════════════════════════════════════════════════════
        // 几何查询
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 获取槽的四个角点（局部坐标，逆时针顺序）
        ///
        ///   P3 ──────────────── P2
        ///   │ ← LeftWidth  RightWidth → │
        ///   │       中心线 →            │
        ///   P0 ──────────────── P1
        ///
        ///   P0 = Start 偏左  P1 = End 偏左
        ///   P2 = End   偏右  P3 = Start 偏右
        /// </summary>
        public (Vec2 P0, Vec2 P1, Vec2 P2, Vec2 P3) GetCorners()
        {
            Vec2 dir = Direction;
            Vec2 leftNorm = new Vec2(-dir.Y, dir.X);   // 左法向（逆时针90°）
            Vec2 rightNorm = new Vec2(dir.Y, -dir.X);   // 右法向（顺时针90°）

            Vec2 p0 = StartPt + leftNorm * LeftWidth;
            Vec2 p1 = EndPt + leftNorm * LeftWidth;
            Vec2 p2 = EndPt + rightNorm * RightWidth;
            Vec2 p3 = StartPt + rightNorm * RightWidth;

            return (p0, p1, p2, p3);
        }

        /// <summary>
        /// 中心线上任意 t ∈ [0,1] 处的中点坐标
        /// </summary>
        public Vec2 GetPointAt(float t) =>
            StartPt + (EndPt - StartPt) * Math.Clamp(t, 0f, 1f);

        // ══════════════════════════════════════════════════════
        // 翻面重映射（重写基类）
        // ══════════════════════════════════════════════════════

        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            StartPt = FlipRemapper.RemapPoint(StartPt, axis, bounds);
            EndPt = FlipRemapper.RemapPoint(EndPt, axis, bounds);
            LocalPos = StartPt;

            // 翻面后方向反向时，左右也随之互换，保持物理意义一致
            // （镜像后左手侧变右手侧）
            if (axis == FlipAxis.AroundX || axis == FlipAxis.AroundY)
                (LeftWidth, RightWidth) = (RightWidth, LeftWidth);

            Face.ApplyFlipSide(
                FlipRemapper.RemapSide(Face.GetCurrentSide(), axis));
        }

        // ══════════════════════════════════════════════════════
        // 信息输出
        // ══════════════════════════════════════════════════════

        public override string GetInfo()
        {
            string widthStr = IsSymmetric
                ? $"W={Width:F2}mm（对称）"
                : $"W={Width:F2}mm（左{LeftWidth:F2} / 右{RightWidth:F2}）";

            return $"[Groove {Id}] Type={GrooveType,-12} " +
                   $"Side={CurrentSide,-8} " +
                   $"{widthStr}  " +
                   $"D={Depth:F2}mm  " +
                   $"L={Length:F2}mm  " +
                   $"Start={StartPt}  End={EndPt}";
        }
    }
}
