using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// PLC 指令 DTO —— 支持 WPF 双向绑定就地编辑
    /// </summary>
    public partial class PlcInstructionDto : ObservableObject
    {
        /// <summary>数据库 Id（用于持久化）</summary>
        public long Id { get; set; }

        [ObservableProperty]
        private int _t;

        [ObservableProperty]
        private int _f;

        [ObservableProperty]
        private int _d;

        [ObservableProperty]
        private float _x0;

        [ObservableProperty]
        private float _y0;

        [ObservableProperty]
        private float _z0;

        [ObservableProperty]
        private float _x1;

        [ObservableProperty]
        private float _y1;

        [ObservableProperty]
        private float _z1;

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>原始 PlcInstruction 转 DTO</summary>
        public static PlcInstructionDto FromPlcInstruction(Plcs.PlcInstruction inst, int sortOrder = 0)
        {
            return new PlcInstructionDto
            {
                T = inst.T,
                F = inst.F,
                D = inst.D,
                X0 = inst.X0,
                Y0 = inst.Y0,
                Z0 = inst.Z0,
                X1 = inst.X1,
                Y1 = inst.Y1,
                Z1 = inst.Z1,
                SortOrder = sortOrder
            };
        }

        /// <summary>DTO 转 PlcInstruction</summary>
        public Plcs.PlcInstruction ToPlcInstruction()
        {
            return new Plcs.PlcInstruction
            {
                T = T,
                F = F,
                D = D,
                X0 = X0,
                Y0 = Y0,
                Z0 = Z0,
                X1 = X1,
                Y1 = Y1,
                Z1 = Z1
            };
        }
    }

    /// <summary>
    /// 墙体信息 DTO —— 用于顶部信息条
    /// </summary>
    public class WallInfoDto
    {
        public long Id { get; set; }
        public string WallId { get; set; } = string.Empty;
        public string WallName { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = "V0.0.0";
        public int AuditStatus { get; set; }
        public bool IsAudited => AuditStatus == 1;
        public string AuditStatusText => IsAudited ? "已审核" : "未审核";
        public string ProjectName { get; set; } = string.Empty;
        public int Floor { get; set; }
        public string BimJsonData { get; set; } = string.Empty;
        public string? MomJsonData { get; set; }
    }

    /// <summary>
    /// 特征分组 DTO —— 用于左侧特征列表
    /// </summary>
    public partial class PlcFeatureGroupDto : ObservableObject
    {
        /// <summary>Handler 名称</summary>
        public string HandlerName { get; set; } = string.Empty;

        /// <summary>特征中文名称</summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>该特征下的指令条数</summary>
        [ObservableProperty]
        private int _instructionCount;

        /// <summary>该特征下的指令列表</summary>
        public List<PlcInstructionDto> Instructions { get; set; } = new();
    }

    /// <summary>
    /// 加工统计 DTO
    /// </summary>
    public partial class PlcStatisticsDto : ObservableObject
    {
        /// <summary>总指令条数</summary>
        [ObservableProperty]
        private int _totalInstructionCount;

        /// <summary>换刀次数（T 值变化次数）</summary>
        [ObservableProperty]
        private int _toolChangeCount;

        /// <summary>总切削面积（平方毫米）</summary>
        [ObservableProperty]
        private double _totalCuttingArea;

        /// <summary>预估工时（分钟）</summary>
        [ObservableProperty]
        private double _estimatedHours;

        /// <summary>预设：标准切削速率（mm²/分钟）</summary>
        public const double StandardCuttingRate = 5000.0;
    }
}
