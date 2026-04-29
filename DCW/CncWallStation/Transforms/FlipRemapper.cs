using CncWallStation.Features;
using Infrastructure.Maths;

namespace CncWallStation.Transforms
{
    /// <summary>
    /// 翻面坐标重映射工具类
    /// 翻面后保证以左下角为加工原点
    /// </summary>
    public static class FlipRemapper
    {
        /// <summary>
        /// 重映射二维点（翻面后坐标系修正）
        /// </summary>
        /// <param name="original">翻面前局部坐标</param>
        /// <param name="axis">翻面轴</param>
        /// <param name="bounds">轮廓包围盒 (minX, minY, maxX, maxY)</param>
        public static Vec2 RemapPoint(
            Vec2 original,
            FlipAxis axis,
            (float minX, float minY, float maxX, float maxY) bounds)
        {
            float W = bounds.maxX - bounds.minX;
            float H = bounds.maxY - bounds.minY;

            return axis switch
            {
                // 绕X轴翻面：Y镜像，X不变
                FlipAxis.AroundX => new Vec2(
                    original.X,
                    H - (original.Y - bounds.minY) + bounds.minY),

                // 绕Y轴翻面：X镜像，Y不变
                FlipAxis.AroundY => new Vec2(
                    W - (original.X - bounds.minX) + bounds.minX,
                    original.Y),

                // 绕Z轴翻面：X和Y都镜像
                FlipAxis.AroundZ => new Vec2(
                    W - (original.X - bounds.minX) + bounds.minX,
                    H - (original.Y - bounds.minY) + bounds.minY),

                _ => original
            };
        }

        /// <summary>
        /// 翻面后加工面映射
        /// </summary>
        public static MachineSide RemapSide(MachineSide side, FlipAxis axis)
        {
            return axis switch
            {
                FlipAxis.AroundX => side switch
                {
                    MachineSide.Top => MachineSide.Bottom,
                    MachineSide.Bottom => MachineSide.Top,
                    MachineSide.Front => MachineSide.Back,
                    MachineSide.Back => MachineSide.Front,
                    _ => side   // Left / Right 不变
                },
                FlipAxis.AroundY => side switch
                {
                    MachineSide.Top => MachineSide.Bottom,
                    MachineSide.Bottom => MachineSide.Top,
                    MachineSide.Left => MachineSide.Right,
                    MachineSide.Right => MachineSide.Left,
                    _ => side   // Front / Back 不变
                },
                FlipAxis.AroundZ => side switch
                {
                    MachineSide.Front => MachineSide.Back,
                    MachineSide.Back => MachineSide.Front,
                    MachineSide.Left => MachineSide.Right,
                    MachineSide.Right => MachineSide.Left,
                    _ => side   // Top / Bottom 不变
                },
                _ => side
            };
        }
    }
}
