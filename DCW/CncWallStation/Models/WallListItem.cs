using CncWallStation.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.Models
{
	/// <summary>
	/// 加工优先级（值越大优先级越高）
	/// </summary>
	public enum ProcessPriority
	{
		低 = 0,
		中 = 1,
		高 = 2
	}

	/// <summary>
	/// 加工状态
	/// </summary>
	public enum ProcessStatus
	{
		待校验 = 0,
		待加工 = 1,
		加工中 = 2,
		已完成 = 3,
		异常 = 4,
		已质检 = 5
	}

	/// <summary>
	/// 墙体清单数据项（MVVM 展示模型）
	/// </summary>
	public partial class WallListItem : ObservableObject
	{
		/// <summary>数据库主键（隐藏列，用于更新/删除操作）</summary>
		public long Id { get; set; }

		/// <summary>项目名称</summary>
		public string ProjectName { get; set; } = string.Empty;

		/// <summary>楼层</summary>
		public int Floor { get; set; }

		/// <summary>墙体ID（唯一标识）</summary>
		public string WallId { get; set; } = string.Empty;

		/// <summary>墙体名称</summary>
		public string WallName { get; set; } = string.Empty;

		/// <summary>导入时间</summary>
		public DateTime ImportTime { get; set; } = DateTime.Now;

		/// <summary>mjson 原始数据（JSON 字符串）</summary>
		public string MjsonData { get; set; } = "{}";

		/// <summary>MomJSON 数据（JSON 字符串，转换后才有值）</summary>
		public string? MomJsonData { get; set; }

		/// <summary>管线阶段</summary>
		public PipelineStage PipelineStage { get; set; } = PipelineStage.Imported;

		/// <summary>管线阶段显示文本</summary>
		public string PipelineStageText => PipelineStage.ToDisplayText();

		/// <summary>加工优先级（int，数值越大优先级越高）</summary>
		public int Priority { get; set; } = 0;

		/// <summary>生产状态</summary>
		public ProcessStatus Status { get; set; } = ProcessStatus.待校验;

		/// <summary>审核状态：0=未审核，1=已审核</summary>
		public int AuditStatus { get; set; } = 0;

		/// <summary>审核状态显示文本</summary>
		public string AuditStatusText => AuditStatusExtensions.FromInt(AuditStatus).ToDisplayText();

		/// <summary>Schema 版本号（来自 BimJson schema 字段）</summary>
		public string SchemaVersion { get; set; } = "V0.0.0";

		/// <summary>开始生产时间</summary>
		public DateTime? StartProductionTime { get; set; }

		/// <summary>结束生产时间</summary>
		public DateTime? EndProductionTime { get; set; }

		/// <summary>软删除标记</summary>
		public bool IsDeleted { get; set; }

		/// <summary>最后更新时间</summary>
		public DateTime UpdatedAt { get; set; } = DateTime.Now;

		/// <summary>最后修改人</summary>
		public string? UpdatedBy { get; set; }

		/// <summary>校验失败原因摘要（多条合并显示）</summary>
		public string? ValidationErrorSummary { get; set; }

		/// <summary>是否被 CheckBox 选中（独立于 DataGrid 行选中）</summary>
		[ObservableProperty]
		private bool _isSelected;
	}

	/// <summary>
	/// 批量导入结果项
	/// </summary>
	public class WallImportResult
	{
		public string FilePath { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public bool Success { get; set; }
		public bool IsDuplicate { get; set; }
		public string Message { get; set; } = string.Empty;
		public WallListItem? Item { get; set; }
	}
}
