using CncWallStation.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 校验/转换失败详情表实体
    /// </summary>
    [Table("ValidationError")]
    public class ValidationErrorEntity
    {
        /// <summary>主键</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>关联墙体（外键 → Wall.Id）</summary>
        [Required]
        public long WallId { get; set; }

        /// <summary>同一次校验/转换操作的分组标识（Guid），用于生成错误报告</summary>
        [Required]
        [MaxLength(64)]
        public string GroupId { get; set; } = string.Empty;

        /// <summary>失败时所处的管线阶段</summary>
        public PipelineStage PipelineStage { get; set; }

        /// <summary>错误码（便于分类统计）</summary>
        [MaxLength(128)]
        public string? ErrorCode { get; set; }

        /// <summary>失败原因详情</summary>
        [Required]
        [Column(TypeName = "text")]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ==================== 导航属性 ====================

        /// <summary>关联的墙体</summary>
        [ForeignKey(nameof(WallId))]
        public WallEntity Wall { get; set; } = null!;
    }
}
