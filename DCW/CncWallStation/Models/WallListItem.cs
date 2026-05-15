using CncWallStation.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.Models
{
	/// <summary>
	/// 加工优先级
	/// </summary>
	public enum ProcessPriority
	{
		高 = 0,
		中 = 1,
		低 = 2
	}

	/// <summary>
	/// 加工状态
	/// </summary>
	public enum ProcessStatus
	{
		待加工 = 0,
		加工中 = 1,
		已完成 = 2,
		异常 = 3
	}

	/// <summary>
	/// 墙体清单数据项（MVVM 展示模型）
	/// </summary>
	public partial class WallListItem : ObservableObject
	{
		/// <summary>数据库主键（隐藏列，用于更新/删除操作）</summary>
		public long Id { get; set; }

		/// <summary>房屋编号 / 项目号</summary>
		public string HouseNumber { get; set; } = string.Empty;

		/// <summary>楼层</summary>
		public int Floor { get; set; }

		/// <summary>墙体ID（唯一标识）</summary>
		public string WallId { get; set; } = string.Empty;

		/// <summary>导入时间</summary>
		public DateTime ImportTime { get; set; } = DateTime.Now;

		/// <summary>mjson 原始数据（JSON 字符串）</summary>
		public string MjsonData { get; set; } = "{}";

		/// <summary>MomJSON 数据（JSON 字符串，转换后才有值）</summary>
		public string? MomJsonData { get; set; }

		/// <summary>管线阶段</summary>
		public PipelineStage PipelineStage { get; set; } = PipelineStage.Imported;

		/// <summary>管线阶段显示文本</summary>
		public string PipelineStageText => PipelineStage switch
		{
			PipelineStage.Imported => "已导入",
			PipelineStage.ValidatingBim => "校验Bim中",
			PipelineStage.BimValid => "Bim校验通过",
			PipelineStage.BimInvalid => "Bim校验失败",
			PipelineStage.Converting => "转换中",
			PipelineStage.ConversionFailed => "转换失败",
			PipelineStage.Converted => "已转换",
			PipelineStage.ValidatingMom => "校验Mom中",
			PipelineStage.MomValid => "Mom校验通过",
			PipelineStage.MomInvalid => "Mom校验失败",
			PipelineStage.Ready => "待加工",
			_ => "未知"
		};

		/// <summary>加工优先级</summary>
		public ProcessPriority Priority { get; set; } = ProcessPriority.中;

		/// <summary>加工状态</summary>
		public ProcessStatus Status { get; set; } = ProcessStatus.待加工;

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
