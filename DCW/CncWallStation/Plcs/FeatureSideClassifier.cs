using CncWallStation.Features;
using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;

namespace CncWallStation.Plcs
{
    /// <summary>
    /// 特征正反面分类器
    /// 基于切削面（InitialSide）和起点Y值判断特征属于正面还是反面
    /// </summary>
    public static class FeatureSideClassifier
    {
        /// <summary>
        /// 判断特征是否属于正面
        /// <para>分类规则：</para>
        /// <para>- Top → 正面</para>
        /// <para>- Bottom → 反面</para>
        /// <para>- Front/Back 面的 Hole（含 Round/Slotted）→ 确保 origin transform 后生产面为 Back（底面）：
        ///   Front 面 → 正面（D=1），Back 面 → 反面（D=5）</para>
        /// <para>- 其他面（Left/Right/Custom）及非 Hole 特征 → 起点Y值 > 墙体厚度一半 → 正面，否则反面</para>
        /// </summary>
        /// <param name="feature">加工特征</param>
        /// <param name="wallThickness">墙体厚度（mm）</param>
        /// <returns>true=正面，false=反面</returns>
        public static bool IsFront(Feature feature, float wallThickness)
        {
            return feature.InitialSide switch
            {
                MachineSide.Top => true,
                MachineSide.Bottom => false,
                MachineSide.Front or MachineSide.Back when feature is Hole => feature.InitialSide == MachineSide.Back,
                _ => GetStartPointY(feature) > wallThickness * 0.5f
            };
        }

        /// <summary>
        /// 获取特征的起点Y值
        /// <para>对 Groove 使用 StartPt.Y，对 MepSlot 使用首段 StartPoint.Y，其余使用 LocalPos.Y</para>
        /// </summary>
        private static float GetStartPointY(Feature feature)
        {
            return feature switch
            {
                Groove groove => groove.StartPt.Y,
                MepSlot mepSlot when mepSlot.Segments.Count > 0 => mepSlot.Segments[0].StartPoint.Y,
                _ => feature.LocalPos.Y
            };
        }
    }
}
