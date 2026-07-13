using CncWallStation.Features;
using CncWallStation.MomWallData;

namespace CncWallStation.Plcs.Handlers
{
    public static class WallHandler
    {
        /// <summary>
        /// 生成墙定义指令
        /// </summary>
        /// <param name="wall">墙体数据</param>
        /// <param name="ctx">转换上下文</param>
        /// <param name="d">墙定义 D 值（正面=1，反面=5）</param>
        public static void Handle(MomWall wall, PlcConvertContext ctx, int d = 1)
        {
            ctx.Emit(new PlcInstruction
            {
                T = PlcTool.Mill,
                F = PlcFeatureCode.WallDefine,
                D = d,
                X0 = wall.ActualLength,
                Y0 = wall.ActualWidth,
                Z0 = wall.ActualThickness,
                X1 = wall.Length,
                Y1 = wall.Width,
                Z1 = wall.Thickness
            });
        }
    }
}
