using CncWallStation.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 墙体数据表实体
    /// </summary>
    [Table("Wall")]
    public class WallEntity
    {
        /// <summary>主键</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>所属导入批次（外键 → Project.Id）</summary>
        [Required]
        public int ProjectId { get; set; }

        /// <summary>墙体唯一标识</summary>
        [Required]
        [MaxLength(256)]
        public string WallId { get; set; } = string.Empty;

        /// <summary>项目号（冗余加速查询）</summary>
        [Required]
        [MaxLength(256)]
        public string ProjectNumber { get; set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; set; }

        /// <summary>原始 BimJSON 数据（最大 16MB）</summary>
        [Required]
        [Column(TypeName = "MEDIUMTEXT")]
        public string BimJsonData { get; set; } = string.Empty;

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; set; } = PipelineStage.Imported;

        /// <summary>转换后的 MomJSON 数据（最大 16MB）</summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string? MomJsonData { get; set; }

        /// <summary>加工优先级枚举值</summary>
        public int Priority { get; set; } = 1;

        /// <summary>加工状态（仅 Ready 后为待加工）</summary>
        public int Status { get; set; } = 0;

        /// <summary>导入时间</summary>
        [Required]
        public DateTime ImportTime { get; set; } = DateTime.Now;

        /// <summary>最后更新时间</summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>最后修改人（当前 Windows 用户）</summary>
        [MaxLength(256)]
        public string? UpdatedBy { get; set; }

        // ==================== 导航属性 ====================

        /// <summary>所属项目批次</summary>
        [ForeignKey(nameof(ProjectId))]
        public ProjectEntity Project { get; set; } = null!;

        /// <summary>校验/转换失败详情列表</summary>
        public ICollection<ValidationErrorEntity> ValidationErrors { get; set; } = new List<ValidationErrorEntity>();
    }
}
