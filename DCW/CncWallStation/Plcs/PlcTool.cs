using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CncWallStation.Plcs
{
    /// <summary>刀具编号（T 值）</summary>
    public static class PlcTool
    {
        public const int Mill = 1;   // 铣刀（抛面/测量）
        public const int RebarCutter = 2;   // 钢筋槽
        public const int LargeDrill = 3;   // 定位孔钻（Φ25）
        public const int StepCutter = 4;   // 台阶（斜撑、侧边台阶）
        public const int Sealing = 6;   // 密封条
        public const int WindowCutter = 7;   // 窗洞
        public const int SlotCutter = 8;   // 线槽 / 线盒 / 斜槽 / 自由槽
        public const int SmallDrill = 9;   // 小孔（Φ12）
        public const int FaceHole = 10;  // 墙面孔
        public const int XpsOffset = 16;  // XPS 偏移 / 胶缝
    }
}
