namespace CncWallStation.Plcs
{
    /// <summary>
    /// PLC 指令
    /// </summary>
    public struct PlcInstruction
    {
        public int T;   // Tool 刀具编号
        public int F;   // Feature 特征子类型
        public int D;   // 重复数量
        public float X0, Y0, Z0;   // 起点 / 基准点
        public float X1, Y1, Z1;   // 对角点 / 尺寸 / 终点

        public string ToCsv() =>
            $"{T},{F},{X0:F1},{Y0:F1},{Z0:F1},{X1:F1},{Y1:F1},{Z1:F1},{D}";

        public override string ToString() =>
            $"T{T} F{F} D{D} P0({X0:F1},{Y0:F1},{Z0:F1}) P1({X1:F1},{Y1:F1},{Z1:F1})";
    }
}
