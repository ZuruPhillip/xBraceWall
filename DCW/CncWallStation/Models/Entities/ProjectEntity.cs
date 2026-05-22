using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 导入批次 / 版本表实体（ABP 风格）
    /// </summary>
    [Table("Project")]
    public class ProjectEntity : Entity<int>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected ProjectEntity() { }

		/// <summary>创建新导入批次</summary>
		public ProjectEntity(
			string projectName,
			string? sourceFolderPath,
			string? hostName,
			string? importedBy,
			int totalWalls)
		{
			ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
			SourceFolderPath = sourceFolderPath;
			HostName = hostName;
			ImportedBy = importedBy;
			TotalWalls = totalWalls;
			ImportTime = DateTime.Now;
		}

        // ==================== 属性 ====================

		/// <summary>项目名称</summary>
		[Required]
		[MaxLength(256)]
		public string ProjectName { get; private set; } = string.Empty;

		/// <summary>源文件夹路径（导入方的本地路径）</summary>
        [MaxLength(1024)]
        public string? SourceFolderPath { get; private set; }

        /// <summary>执行导入的主机名</summary>
        [MaxLength(256)]
        public string? HostName { get; private set; }

        /// <summary>导入者标识（当前 Windows 用户）</summary>
        [MaxLength(256)]
        public string? ImportedBy { get; private set; }

        /// <summary>该版本墙体总数</summary>
        public int TotalWalls { get; private set; }

        /// <summary>导入时间</summary>
        [Required]
        public DateTime ImportTime { get; private set; } = DateTime.Now;

        /// <summary>备注</summary>
        [Column(TypeName = "text")]
        public string? Notes { get; private set; }

        /// <summary>该版本下的墙体列表</summary>
        public ICollection<WallEntity> Walls { get; private set; } = new List<WallEntity>();

		// ==================== 领域方法 ====================

		/// <summary>设置备注</summary>
		public void SetNotes(string? notes)
		{
			Notes = notes;
		}
	}
}
