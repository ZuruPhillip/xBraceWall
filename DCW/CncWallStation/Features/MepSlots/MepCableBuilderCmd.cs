using Infrastructure.Maths;

namespace CncWallStation.Features.MepSlots
{
    public class MepCableBuilderCmd
    {
        /// <summary>MepCable → MepSlot 转换过程中的中间构建命令</summary>
        internal interface IBuildCmd { }

        /// <summary>普通直线段命令</summary>
        internal record CmdLine(Vec2 Start, Vec2 End, float Depth)
            : IBuildCmd;

        /// <summary>宽度覆盖直线段命令（waffleBox 段）</summary>
        internal record CmdWideLine(Vec2 Start, Vec2 End, float Depth, float Width)
            : IBuildCmd;

        /// <summary>渐变深度直线段命令（device 段）</summary>
        internal record CmdTaperLine(Vec2 Start, Vec2 End,
                                      float DepthStart, float DepthEnd)
            : IBuildCmd;

        /// <summary>圆弧段命令（corner 倒角）</summary>
        internal record CmdArc(Vec2 Center, float Radius,
                                float StartAngleDeg, float EndAngleDeg,
                                float Depth, bool IsClockwise)
            : IBuildCmd;

        /// <summary>原始折线段（ProcessCorners 入参）</summary>
        internal record RawLine(Vec2 Start, Vec2 End, float Depth);
    }
}
