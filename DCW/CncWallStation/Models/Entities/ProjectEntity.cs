using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 导入批次 / 版本表实体
    /// </summary>
    [Table("Project")]
    public class ProjectEntity
    {
        /// <summary>主键</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>项目号</summary>
        [Required]
        [MaxLength(256)]
        public string ProjectNumber { get; set; } = string.Empty;

        /// <summary>同一项目版本号，默认 1</summary>
        public int Version { get; set; } = 1;

        /// <summary>是否最新版本</summary>
        public bool IsLatest { get; set; } = true;

        /// <summary>源文件夹路径（导入方的本地路径）</summary>
        [MaxLength(1024)]
        public string? SourceFolderPath { get; set; }

        /// <summary>执行导入的主机名</summary>
        [MaxLength(256)]
        public string? HostName { get; set; }

        /// <summary>导入者标识（当前 Windows 用户）</summary>
        [MaxLength(256)]
        public string? ImportedBy { get; set; }

        /// <summary>该版本墙体总数</summary>
        public int TotalWalls { get; set; }

        /// <summary>导入时间</summary>
        [Required]
        public DateTime ImportTime { get; set; } = DateTime.Now;

        /// <summary>备注</summary>
        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        /// <summary>该版本下的墙体列表</summary>
        public ICollection<WallEntity> Walls { get; set; } = new List<WallEntity>();
    }
}
