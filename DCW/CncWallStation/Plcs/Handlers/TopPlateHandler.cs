using CncWallStation.Consts;
using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.MomWallData;
using static CncWallStation.Plcs.GrooveSideClassifier;

namespace CncWallStation.Plcs.Handlers
{
    // ══════════════════════════════════════════════════════
    // T6 顶板槽处理（TopPlate）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 顶板槽加工（T6）
    ///
    /// 规则：
    ///   • 底部密封条（F0）：起点 = (0, 0, 107)
    ///   • 顶部密封条（F2）：起点 = (wallLength, wallWidth, 107)
    ///   • X1 = 墙长度
    ///   • Y1 = 槽厚度（即 groove.Width，切深方向）
    ///   • Z1 = 12（刀具厚度，固定）
    /// </summary>
    public static class TopPlateHandler
    {
        public static void Handle(Groove g, PlcConvertContext ctx, MomWall wall)
        {
            if (g.GrooveType is not GrooveType.TopPlate) return;

            // 根据 InitialSide 决定加工方向
            //   Front → Top,  Back → Bottom
            GrooveSide side = g.InitialSide switch
            {
                MachineSide.Front => GrooveSide.Top,
                _ => GrooveSide.None
            };
            if (side == GrooveSide.None)
            {
                // 顶板槽只能贴在墙顶或墙底
                return;
            }

            float wallLength = wall.Length;
            float wallWidth = wall.Width;


            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.SlotCutter,    // T8
                F = 10, //F10
                D = 0,
                X0 = 0f,
                Y0 = wallWidth - WallConstants.TopPlateGrooveDepth,
                Z0 = 0f,
                X1 = wallLength,// 墙长度
                Y1 = WallConstants.TopPlateGrooveDepth,                     
                Z1 = WallConstants.TopPlateGrooveWidth 
            });
        }
    }
}
