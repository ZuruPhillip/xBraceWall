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
	/// 墙体清单数据项
	/// </summary>
	public partial class WallListItem : ObservableObject
	{
		/// <summary>房屋编号</summary>
		public string HouseNumber { get; set; } = string.Empty;

		/// <summary>楼层</summary>
		public int Floor { get; set; }

		/// <summary>墙体ID（唯一标识）</summary>
		public string WallId { get; set; } = string.Empty;

		/// <summary>导入时间</summary>
		public DateTime ImportTime { get; set; } = DateTime.Now;

		/// <summary>mjson 原始数据（JSON 字符串）</summary>
		public string MjsonData { get; set; } = "{}";

		/// <summary>加工优先级</summary>
		public ProcessPriority Priority { get; set; } = ProcessPriority.中;

		/// <summary>加工状态</summary>
		public ProcessStatus Status { get; set; } = ProcessStatus.待加工;

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
