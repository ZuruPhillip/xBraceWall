using CncWallStation.Consts;
using CncWallStation.MomWallData;
using Infrastructure.Maths;

namespace CncWallStation.Features.Props
{
    /// <summary>
    /// 斜撑槽（Propping）转换器
    ///
    /// 几何规则：
    ///   • Top 面：八边形轮廓
    ///   • Side 面：八边形轮廓
    ///
    /// 双面切割组合：
    ///   Top（XY 平面，深度沿 -Z）+ Side（XZ 平面，深度沿 -Y）
    ///
    /// </summary>
    internal static class ProppingConverter
    {
        // ═══════════════════════════════════════════════════════════════
        // 公开入口
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 根据斜撑 Top 面中间点 X 坐标构造单条斜撑槽
        /// </summary>
        /// <param name="centerX">斜撑 Top 面中心 X 坐标（墙局部坐标）</param>
        /// <param name="centerY">斜撑 Top 面中心 Y 坐标（墙局部坐标）</param>
        /// <param name="momWallData">目标墙体</param>
        /// <param name="id">特征 ID，默认 Propping-{X}</param>
        public static void Convert(
            float centerX,
            MomWall momWallData,
            string? id = null)
        {
            id ??= $"Propping-{centerX:F0}";

            // ── 1. Top 面八边形（XY 平面） ───────────────────────────
            var topOutline = BuildTopOutline(centerX,momWallData.Width);

            // ── 2. Side 面八边形（XZ 平面）───────────────────────────
            //     Side 面中心 X = centerX，中心 Z = Wall.Thickness / 2 或自定义
            //     这里 XZ 局部坐标：X 对应墙 X，Y 对应 Z 深度
            var sideOutline = BuildSideOutline(centerX, momWallData.Thickness);

            // ── 3. 使用 CreateDualFace 组装 ──────────────────────────
            var strut = Propping.CreateDualFace(
                id: id,
                topOutline: topOutline,
                topDepth: WallConstants.ProppingTopSlotDepth,
                frontOutline: sideOutline,
                frontDepth: WallConstants.ProppingSideSlotDepth,
                propType: PropType.General);

            momWallData.Features.Add(strut);
        }

        // ═══════════════════════════════════════════════════════════════
        // Top 面八边形轮廓（两端宽、中间窄的哑铃形）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 构造 Top 面哑铃形轮廓（8 顶点，逆时针）
        ///
        ///   约束：
        ///     • 槽顶边贴墙顶：Y_max = wallWidth
        ///     • 槽底边     ：Y_min = wallWidth - ProppingTopSlotLength
        ///     • 水平方向以 x 为对称中心
        ///     • 中间窄部在长度方向居中，宽端直接过渡到窄端（无斜角/收口）
        ///
        ///          Y
        ///          ↑
        ///    P7 ─────── P6       ← yTop       = wallWidth
        ///    │            │
        ///    │            │         上端宽部
        ///    │            │
        ///    P0           P5      ← yMidTop    = wallWidth - (totalL - midL)/2
        ///     │          │
        ///     │          │          中间窄部
        ///     │          │
        ///    P1           P4      ← yMidBottom = wallWidth - (totalL + midL)/2
        ///    │            │
        ///    │            │         下端宽部
        ///    │            │
        ///    P2 ─────── P3       ← yBottom    = wallWidth - totalL
        ///    ←── totalW ──→
        /// </summary>
        private static List<Vec2> BuildTopOutline(float centerX, float wallWidth)
        {
            float totalW = WallConstants.ProppingTopSlotWidth;      // 100
            float midW = WallConstants.ProppingTopSlotMidWidth;   //  60
            float totalL = WallConstants.ProppingTopSlotLength;     // 300
            float midL = WallConstants.ProppingTopSlotMidLength;  // 200

            float halfTotalW = totalW * 0.5f;   // 50
            float halfMidW = midW * 0.5f;   // 30
            float shoulder = (totalL - midL) * 0.5f; // 50（两端宽段的长度）

            // ── Y 坐标（顶→底）────────────────────────────────
            float yTop = wallWidth;
            float yMidTop = wallWidth - shoulder;
            float yMidBottom = wallWidth - shoulder - midL;
            float yBottom = wallWidth - totalL;

            // ── X 坐标 ───────────────────────────────────────
            float xL_out = centerX - halfTotalW;   // 外部左
            float xL_in = centerX - halfMidW;     // 内部左
            float xR_in = centerX + halfMidW;     // 内部右
            float xR_out = centerX + halfTotalW;   // 外部右

            // ── 8 顶点（逆时针，从中间窄部左上开始）──────────
            return new List<Vec2>
            {
                new Vec2(xL_in,  yMidTop),     // P0  窄部左上
                new Vec2(xL_in,  yMidBottom),  // P1  窄部左下
                new Vec2(xL_out, yBottom),     // P2  下端左外角
                new Vec2(xR_out, yBottom),     // P3  下端右外角
                new Vec2(xR_in,  yMidBottom),  // P4  窄部右下
                new Vec2(xR_in,  yMidTop),     // P5  窄部右上
                new Vec2(xR_out, yTop),        // P6  上端右外角
                new Vec2(xL_out, yTop),        // P7  上端左外角
            };
        }

        /// <summary>
        /// 构造 Side 面倒 T 形（⊥）轮廓（8 顶点，逆时针）
        ///
        ///   约束：
        ///     • 下端贴墙厚底部：Z_max = wallThickness      （窄段 EdgeWidth）
        ///     • 上端顶边      ：Z_min = wallThickness - SideSlotLength （宽段 SideWidth）
        ///     • 窄段长度      ：EdgeLength
        ///     • 水平方向以 centerX 为对称中心（下端中心）
        ///
        ///           Z
        ///           ↑
        ///   P4 ─────────────── P3   ← zTop      = wallThickness - SideSlotLength
        ///   │                   │
        ///   │                   │     宽段 SideWidth (300)
        ///   │                   │
        ///   P5 ─┐           ┌── P2  ← zShoulder = wallThickness - EdgeLength
        ///       │           │
        ///       │           │         窄段 EdgeWidth (100)
        ///       │           │
        ///       P6 ─────── P1        ← zBottom  = wallThickness
        ///       ← EdgeW →
        ///   ←──── SideWidth ────→
        ///   
        /// </summary>
        private static List<Vec2> BuildSideOutline(float centerX, float wallThickness)
        {
            float sideW = WallConstants.ProppingSideSlotWidth;      // 300  宽段宽
            float edgeW = WallConstants.ProppingSideSlotEdgeWidth;  // 100  窄段宽
            float sideL = WallConstants.ProppingSideSlotLength;     // 100  总长
            float edgeL = WallConstants.ProppingSideSlotEdgeLength; //  30  窄段长

            float halfSideW = sideW * 0.5f;   // 150
            float halfEdgeW = edgeW * 0.5f;   //  50

            // ── Z 坐标（底→顶）───────────────────────────
            float zBottom = wallThickness;                  // 窄段底（贴墙底）
            float zShoulder = wallThickness - edgeL;          // 窄段与宽段分界
            float zTop = wallThickness - sideL;          // 宽段顶

            // ── X 坐标 ──────────────────────────────────
            float xL_out = centerX - halfSideW;   // 宽段左外
            float xL_in = centerX - halfEdgeW;   // 窄段左
            float xR_in = centerX + halfEdgeW;   // 窄段右
            float xR_out = centerX + halfSideW;   // 宽段右外

            // ── 8 顶点（逆时针，从窄段右下角开始）─────────
            return new List<Vec2>
            {
                new Vec2(xR_in,  zBottom),    // P1  窄段右下
                new Vec2(xR_in,  zShoulder),  // P2  窄段右肩
                new Vec2(xR_out, zShoulder),  // P3  宽段右下
                new Vec2(xR_out, zTop),       // P4  宽段右上
                new Vec2(xL_out, zTop),       // P5  宽段左上
                new Vec2(xL_out, zShoulder),  // P6  宽段左下
                new Vec2(xL_in,  zShoulder),  // P7  窄段左肩
                new Vec2(xL_in,  zBottom),    // P8  窄段左下
            };
        }
    }
}
