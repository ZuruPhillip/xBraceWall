using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 通用分页结果
    /// </summary>
    public class PagedResult<T>
    {
        /// <summary>总记录数</summary>
        public int TotalCount { get; set; }

        /// <summary>当前页数据</summary>
        public List<T> Items { get; set; } = new();
    }

    /// <summary>
    /// 墙体队列项 DTO
    /// </summary>
    public class WallQueueItemDto
    {
        /// <summary>数据库主键</summary>
        public long Id { get; set; }

        /// <summary>墙体唯一标识</summary>
        public string WallId { get; set; } = string.Empty;

        /// <summary>项目名称</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; set; }

        /// <summary>加工优先级（数值越大越高）</summary>
        public int Priority { get; set; }

        /// <summary>加工状态</summary>
        public int Status { get; set; }

        /// <summary>优先级显示文本</summary>
        public string PriorityText => Priority switch
        {
            2 => "高",
            1 => "中",
            _ => "低"
        };
    }

    /// <summary>
    /// 实时参数 DTO（WPF 双向绑定）
    /// </summary>
    public partial class RealtimeParamsDto : ObservableObject
    {
        /// <summary>工作台是否就位</summary>
        [ObservableProperty]
        private bool _tableReady;

        /// <summary>安全门是否关闭</summary>
        [ObservableProperty]
        private bool _safetyDoorClosed;

        /// <summary>主轴转速（RPM）</summary>
        [ObservableProperty]
        private double _spindleSpeed;

        /// <summary>进给速度（mm/min）</summary>
        [ObservableProperty]
        private double _feedRate;

        /// <summary>当前刀具编号</summary>
        [ObservableProperty]
        private int _currentTool;
    }

    /// <summary>
    /// 实时参数节点 DTO（控制页面"实时参数"面板动态绑定）
    /// </summary>
    public partial class RealtimeNodeItemDto : ObservableObject
    {
        /// <summary>OPC 节点 ID</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>显示名称（取自节点描述，无则用 NodeId）</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>当前值的显示文本</summary>
        [ObservableProperty]
        private string _displayValue = "--";

        /// <summary>质量指示灯颜色：Good=#52C41A, Bad=#E60012, 无数据=#9E9E9E</summary>
        [ObservableProperty]
        private string _qualityColor = "#9E9E9E";
    }

    /// <summary>
    /// PLC 数据行 DTO（用于 PLC 数据 Tab 的 DataGrid 绑定）
    /// </summary>
    public partial class PlcLineDataDto : ObservableObject
    {
        /// <summary>行序号（对应 LineDef 索引 i）</summary>
        public int Index { get; set; }

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

        /// <summary>该行数据是否已加工完成</summary>
        [ObservableProperty]
        private bool _isCompleted;
    }

    /// <summary>
    /// 异常报告 DTO（包含 Wall 表的字符串 WallId）
    /// </summary>
    public class ExceptionReportDto
    {
        public long Id { get; set; }
        public long WallId { get; set; }
        /// <summary>墙体字符串标识（来自 Wall 表）</summary>
        public string WallIdStr { get; set; } = string.Empty;
        public int ExceptionType { get; set; }
        public string? CustomType { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PhotoPaths { get; set; }
        public string Registrant { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime OccurredAt { get; set; }
        public int FrequencyCount { get; set; }
        public bool IsResolved { get; set; }
        public string? RepairMethod { get; set; }
        public string? Resolver { get; set; }
        public decimal? RepairDuration { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string? ImprovementSuggestion { get; set; }
        public string? Remarks { get; set; }
    }
}
