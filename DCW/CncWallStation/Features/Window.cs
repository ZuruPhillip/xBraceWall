using CncWallStation.Transforms;
using Infrastructure.Maths;
using System.Text.Json.Serialization;

namespace CncWallStation.Features
{
    /// <summary>
    /// 窗洞 / 开洞特征（贯穿型多边形开口）
    ///
    /// 几何描述：
    ///   • Contour   — 轮廓点列表（墙体局部坐标，俯视图 XY，闭合多边形）
    ///   • LocalPos  — 轮廓包围盒的左下角点（继承自 Feature，作为定位基准）
    ///   • Depth     — 穿透深度（通常等于墙厚）
    /// </summary>
    public class Window : Feature
    {
        // ──────────────────────────────────────────────
        // 几何属性
        // ──────────────────────────────────────────────

        /// <summary>轮廓点（墙体局部坐标，闭合多边形，按顺序排列）</summary>
        public List<Vec2> Contour { get; set; } = new();

        // ──────────────────────────────────────────────
        // 计算属性
        // ──────────────────────────────────────────────

        /// <summary>轮廓点数量</summary>
        [JsonIgnore]
        public int VertexCount => Contour?.Count ?? 0;

        /// <summary>包围盒 (minX, minY, maxX, maxY)</summary>
        [JsonIgnore]
        public (float MinX, float MinY, float MaxX, float MaxY) BBox => ComputeBBox(Contour);

        /// <summary>窗洞宽度（X 方向）</summary>
        [JsonPropertyName("width")]
        public float Width
        {
            get { var b = BBox; return b.MaxX - b.MinX; }
        }

        /// <summary>窗洞高度（Y 方向）</summary>
        [JsonPropertyName("height")]
        public float Height
        {
            get { var b = BBox; return b.MaxY - b.MinY; }
        }

        /// <summary>左下角点（即 LocalPos 的语义别名，便于阅读）</summary>
        [JsonIgnore]
        public Vec2 BottomLeft => LocalPos;

        /// <summary>右上角点</summary>
        [JsonIgnore]
        public Vec2 TopRight
        {
            get { var b = BBox; return new Vec2(b.MaxX, b.MaxY); }
        }

        // ──────────────────────────────────────────────
        // 构造
        // ──────────────────────────────────────────────

        public Window(string id,
                      MachineSide side,
                      List<Vec2> contour,
                      float depth)
            : base(id, FeatureType.Window, side, ComputeBottomLeft(contour), depth)
        {
            Contour = contour ?? new List<Vec2>();
        }

        // ──────────────────────────────────────────────
        // 翻面：轮廓点 + LocalPos 重新计算
        // ──────────────────────────────────────────────

        internal override void ApplyFlip(
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            // 翻转所有轮廓点
            for (int i = 0; i < Contour.Count; i++)
                Contour[i] = FlipRemapper.RemapPoint(Contour[i], axis, bounds);

            // 翻面后包围盒变化，LocalPos 需要重新计算为新的左下角
            // 不调用 base.ApplyFlip 的 LocalPos 重映射逻辑（因为左下角不是简单镜像点）
            LocalPos = ComputeBottomLeft(Contour);

            // 仅更新加工面方向
            Face.ApplyFlipSide(
                FlipRemapper.RemapSide(Face.GetCurrentSide(), axis));
        }

        // ──────────────────────────────────────────────
        // 工具方法
        // ──────────────────────────────────────────────

        /// <summary>计算轮廓包围盒</summary>
        private static (float MinX, float MinY, float MaxX, float MaxY) ComputeBBox(List<Vec2> contour)
        {
            if (contour == null || contour.Count == 0)
                return (0, 0, 0, 0);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in contour)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return (minX, minY, maxX, maxY);
        }

        /// <summary>计算轮廓包围盒的左下角点</summary>
        private static Vec2 ComputeBottomLeft(List<Vec2> contour)
        {
            var b = ComputeBBox(contour);
            return new Vec2(b.MinX, b.MinY);
        }

        public override string GetInfo() =>
            $"[Window {Id}] BL={LocalPos}, Size={Width:F1}×{Height:F1}, " +
            $"Depth={Depth}, Vertices={VertexCount}";
    }
}