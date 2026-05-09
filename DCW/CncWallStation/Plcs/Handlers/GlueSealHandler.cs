using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;
using static CncWallStation.Plcs.GrooveSideClassifier;

namespace CncWallStation.Plcs.Handlers
{
    // ══════════════════════════════════════════════════════
    // T6 胶缝密封条处理（GlueSeal）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 胶缝密封条加工（T6）
    ///
    /// 规则：
    ///   • 底部密封条（F0）：起点 = (0, 0, 107)
    ///   • 顶部密封条（F2）：起点 = (wallLength, wallWidth, 107)
    ///   • X1 = 墙长度
    ///   • Y1 = 槽厚度（即 groove.Width，切深方向）
    ///   • Z1 = 12（刀具厚度，固定）
    /// </summary>
    public static class GlueSealHandler
    {
        public static void Handle(Groove g, PlcConvertContext ctx, MomWall wall)
        {
            if (g.GrooveType is not GrooveType.GlueSeal) return;

            // 判断在底部还是顶部（仅 Bottom/Top 有效）
            GrooveSide side = GrooveSideClassifier.Classify(g, wall);
            if (side != GrooveSide.Bottom && side != GrooveSide.Top)
            {
                // 胶缝密封条只能贴在墙顶或墙底
                return;
            }

            float wallLength = wall.Length;
            float wallWidth = wall.Width;

            // 按上 / 下两种情况装填
            (int f, float x0, float y0) = side switch
            {
                // 底部密封条 → F0，
                GrooveSide.Bottom => (PlcFeatureCode.SealBottom, 0f, 0f),

                // 顶部密封条 → F2，
                GrooveSide.Top => (PlcFeatureCode.SealTop, wallLength, wallWidth),

                _ => throw new InvalidOperationException(
                         $"GlueSeal 仅支持 Bottom/Top，当前: {side}")
            };

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.Sealing,    // T6
                F = f,
                D = 0,
                X0 = x0,
                Y0 = y0,
                Z0 = WallConstants.GlueSealEdgeDistance,
                X1 = wallLength,                // 墙长度
                Y1 = WallConstants.GlueSealDepth,                             // 槽厚度（切深 15）//g.Depth, 
                Z1 = WallConstants.GlueSealToolThickness                     // 12（刀具厚度）
            });
        }
    }
}
