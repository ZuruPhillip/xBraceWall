namespace CncWallStation.Plcs
{
    /// <summary>
    /// 墙体 → PLC 转换上下文：保存原始尺寸 / 目标尺寸 / 补偿信息
    /// </summary>
    public class PlcConvertContext
    {
        public float RawLength;      // 原始长 (X)
        public float RawHeight;      // 原始高 (Y)
        public float RawThickness;   // 原始厚 (Z)

        public float TargetLength;   // 目标长
        public float TargetHeight;   // 目标高
        public float TargetThickness;// 目标厚

        public float MillOffsetXL;   // 左侧铣去量
        public float MillOffsetXR;
        public float MillOffsetYTop;
        public float MillOffsetYBottom;
        public float MillOffsetZ;

        public bool HasXps;
        public float XpsLeftOffset;   //
        public float XpsRightOffset;  //
        public float XpsYExpand;      //
        public float XpsZOverCut;     //

        public List<PlcInstruction> Output { get; } = new();

        public void Emit(PlcInstruction ins) => Output.Add(ins);
    }
}
