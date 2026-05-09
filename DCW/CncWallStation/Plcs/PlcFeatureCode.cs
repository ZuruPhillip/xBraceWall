namespace CncWallStation.Plcs
{
    /// <summary>特征子类型（F 值）</summary>
    public static class PlcFeatureCode
    {
        // T1
        public const int WallDefine = 0;   // 墙定义（抛面/外轮廓/激光测量）
        public const int CustomFace = 9;   // T1F9 自定义抛面

        // T2  钢筋槽
        public const int RebarVert = 6;   // 纵槽
        public const int RebarHorz = 5;   // 横槽
        public const int RebarVertCont = 16;  // 纵槽连续
        public const int RebarHorzCont = 15;  // 横槽连续

        // T3  定位孔
        public const int PinBottom = 2;   // 底部定位孔
        public const int PinSideCustom = 5;   // 侧面自定义宽度定位孔

        // T4  台阶（方位编码 1~8）
        public const int StepBottomLeft = 0;   // 左下
        public const int StepLeft = 1;   // 左侧
        public const int StepBottomRight = 2;   // 右下
        public const int StepBottomY = 3;   // 底（沿 Y 补偿 -50）

        public const int StepBtm_Auto = 5;   // T4F5 自动补偿 Y -50
        public const int StepLeft_Auto = 6;   // T4F6 自动补偿 X -50
        public const int StepTop_Auto = 7;   // T4F7 自动补偿 Y +50
        public const int StepRight_Auto = 8;   // T4F8 自动补偿 X +50

        // T6 密封条
        public const int SealBottom = 0;
        public const int SealTop = 2;

        // T7 窗户
        public const int Window = 1;

        // T8 线槽/盒  & 斜槽
        public const int CableBox = 10;   // 线盒
        public const int CableSlotL5 = 5;    // L 型 5
        public const int CableSlotL6 = 6;    // L 型 6
        public const int CableSlotL7 = 7;    // L 型 7
        public const int CableSlotL8 = 8;    // L 型 8
        public const int DiagLU_RD = 31;   // 左上→右下
        public const int DiagLD_RU = 30;   // 左下→右上
        public const int DiagWideLD_RU = 32;   // 左下→右上 宽槽
        public const int DiagWideLU_RD = 33;   // 左上→右下 宽槽
        public const int FreeSlot = 39;   // 自由槽

        // T9 底部圆孔
        public const int BottomHole = 9;

        // T10 墙面孔
        public const int FaceHoleCode = 9;
        // 定位孔
        public const int PositionCode = 0;


        // T16  XPS / 胶缝
        public const int XpsOffset = 2;
        public const int GlueSeam = 3;
    }
}
