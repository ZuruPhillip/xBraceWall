namespace CncWallStation.Plcs
{
    /// <summary>
    /// PLC 指令生成结果容器
    /// 包含正面和反面两组指令分组
    /// </summary>
    public class PlcGenerationResult
    {
        /// <summary>正面特征分组（墙定义 D=1）</summary>
        public List<PlcFeatureGroup> FrontGroups { get; set; } = new();

        /// <summary>反面特征分组（墙定义 D=5）</summary>
        public List<PlcFeatureGroup> BackGroups { get; set; } = new();
    }
}
