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
    ///   • 底部密封条（F0）：起点 = (0, 0)
    ///   • 顶部密封条（F2）：起点 = (wallLength, wallWidth)
    ///   • X1 = 墙长度
    ///   • Z0 = 密封槽距顶面距离 = wall.Thickness - (groove.Y + width/2)
    ///   • Y1 = 切削深度
    ///   • Z1 = 切削宽度
    /// </summary>
    public static class GlueSealHandler
    {
        public static void Handle(Groove g, PlcConvertContext ctx, MomWall wall)
        {
            if (g.GrooveType is not GrooveType.GlueSeal) return;

            // 根据 InitialSide 决定加工方向
            //   Front → Top,  Back → Bottom
            GrooveSide side = g.InitialSide switch
            {
                MachineSide.Front => GrooveSide.Top,
                MachineSide.Back => GrooveSide.Bottom,
                _ => GrooveSide.None
            };
            if (side == GrooveSide.None)
            {
                // 胶缝密封条只能贴在墙顶或墙底
                return;
            }

            float wallLength = wall.Length;
            float wallWidth = wall.Width;

            // 按上 / 下两种情况装填
            (int f, float x0, float y0) = side switch
            {
                // 底部密封条 → F0
                GrooveSide.Bottom => (PlcFeatureCode.SealBottom, 0f, 0f),

                // 顶部密封条 → F2
                GrooveSide.Top => (PlcFeatureCode.SealTop, wallLength, wallWidth),

                _ => throw new InvalidOperationException(
                         $"GlueSeal 仅支持 Bottom/Top，当前: {side}")
            };

            // Z0 = 密封槽距顶面距离 = wall.Thickness - (groove.Y + width/2)
            float z0 = wall.Thickness - (g.StartPt.Y + g.Width / 2f);

            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.Sealing,    // T6
                F = f,
                D = 0,
                X0 = x0,
                Y0 = y0,
                Z0 = z0,
                X1 = wallLength,                        // 墙长度
                Y1 = WallConstants.GlueSealGrooveDepth, // 切削深度
                Z1 = WallConstants.GlueSealGrooveWidth  // 切削宽度
            });
        }
    }
}
