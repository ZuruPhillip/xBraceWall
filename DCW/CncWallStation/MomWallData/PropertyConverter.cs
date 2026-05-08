using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CncWallStation.MomWallData
{
    public static class PropertyConverter
    {
        /// <summary>
        /// 判断钢柱位于墙体左侧还是右侧
        /// </summary>
        /// <param name="startPointX">柱子起点 X 坐标（来自 col.StartPoint.X）</param>
        /// <param name="wall">墙体对象</param>
        /// <param name="edgeThreshold">
        /// 边缘判断阈值（mm），默认 100mm
        /// X &lt; edgeThreshold          → 左侧
        /// X &gt; Length - edgeThreshold → 右侧
        /// 否则                          → 中间（非边缘柱）
        /// </param>
        /// <returns><see cref="ColumnSide"/></returns>
        public static ColumnSide DetermineColumnSide(
            float startPointX,
            MomWall wall,
            float edgeThreshold = 100f)
        {
            if (startPointX < edgeThreshold)
                return ColumnSide.Left;

            if (startPointX > wall.Length - edgeThreshold)
                return ColumnSide.Right;

            return ColumnSide.Middle;
        }

        /// <summary>
        /// 钢柱在墙体中的水平位置
        /// </summary>
        public enum ColumnSide
        {
            /// <summary>左侧柱（X &lt; edgeThreshold）</summary>
            Left,

            /// <summary>右侧柱（X &gt; Length - edgeThreshold）</summary>
            Right,

            /// <summary>中间柱（不在任何边缘区域）</summary>
            Middle
        }
    }
}
