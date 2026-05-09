using Infrastructure.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CncWallStation.Features.Props
{
    /// <summary>
    /// 单次平面切割
    /// Propping 的一次组成：从某一面下刀，切出一个统一深度的多边形槽
    /// </summary>
    public class PropCut
    {
        /// <summary>加工面（Top / Front / Back / Bottom / Left / Right）</summary>
        public MachineSide Side { get; set; }

        /// <summary>
        /// 多边形轮廓（加工面的局部 2D 坐标，闭合，逆时针）
        ///
        /// 坐标含义：
        ///   Top/Bottom 面 → (X, Y)    —— XY 平面
        ///   Front/Back 面 → (X, Z)    —— XZ 平面
        ///   Left/Right 面 → (Y, Z)    —— YZ 平面
        /// </summary>
        public List<Vec2> Outline { get; set; } = new();

        /// <summary>统一切割深度（沿面法向量，mm）</summary>
        public float Depth { get; set; }

        /// <summary>该次切割是否贯通墙体厚度</summary>
        [JsonPropertyName("isThrough")]
        public bool IsThrough { get; set; } = false;

        // ── 派生属性 ─────────────────────────────────────────

        [JsonPropertyName("vertexCount")]
        public int VertexCount => Outline.Count;

        [JsonIgnore]
        public (Vec2 Min, Vec2 Max) Bounds
        {
            get
            {
                if (Outline.Count == 0) return (Vec2.Zero, Vec2.Zero);
                float minX = Outline[0].X, minY = Outline[0].Y;
                float maxX = minX, maxY = minY;
                foreach (var p in Outline)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }
                return (new Vec2(minX, minY), new Vec2(maxX, maxY));
            }
        }

        [JsonPropertyName("area")]
        public float Area
        {
            get
            {
                if (Outline.Count < 3) return 0f;
                float sum = 0f;
                for (int i = 0; i < Outline.Count; i++)
                {
                    Vec2 a = Outline[i];
                    Vec2 b = Outline[(i + 1) % Outline.Count];
                    sum += a.X * b.Y - b.X * a.Y;
                }
                return MathF.Abs(sum) * 0.5f;
            }
        }

        // ── 构造 ─────────────────────────────────────────────

        public PropCut() { }

        public PropCut(MachineSide side, List<Vec2> outline, float depth, bool isThrough = false)
        {
            Side = side;
            Outline = new List<Vec2>(outline);
            Depth = depth;
            IsThrough = isThrough;
        }

        public override string ToString() =>
            $"Cut[Side={Side}  Verts={VertexCount}  D={Depth:F2}mm" +
            $"{(IsThrough ? "  THROUGH" : "")}]";
    }
}
