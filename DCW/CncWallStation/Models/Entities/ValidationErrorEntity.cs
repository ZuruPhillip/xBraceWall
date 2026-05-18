using CncWallStation.Models.Enums;
using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 校验/转换失败详情表实体（ABP 风格）
    /// </summary>
    [Table("ValidationError")]
    public class ValidationErrorEntity : Entity<long>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected ValidationErrorEntity() { }

        /// <summary>创建校验错误记录</summary>
        public ValidationErrorEntity(
            long wallId,
            string groupId,
            PipelineStage pipelineStage,
            string errorMessage,
            string? errorCode = null)
        {
            WallId = wallId;
            GroupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            PipelineStage = pipelineStage;
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            ErrorCode = errorCode;
            CreatedAt = DateTime.Now;
        }

        // ==================== 属性 ====================

        /// <summary>关联墙体（外键 → Wall.Id）</summary>
        [Required]
        public long WallId { get; private set; }

        /// <summary>同一次校验/转换操作的分组标识（Guid），用于生成错误报告</summary>
        [Required]
        [MaxLength(64)]
        public string GroupId { get; private set; } = string.Empty;

        /// <summary>失败时所处的管线阶段</summary>
        public PipelineStage PipelineStage { get; private set; }

        /// <summary>错误码（便于分类统计）</summary>
        [MaxLength(128)]
        public string? ErrorCode { get; private set; }

        /// <summary>失败原因详情</summary>
        [Required]
        [Column(TypeName = "text")]
        public string ErrorMessage { get; private set; } = string.Empty;

        /// <summary>创建时间</summary>
        [Required]
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        // ==================== 导航属性 ====================

        /// <summary>关联的墙体</summary>
        [ForeignKey(nameof(WallId))]
        public WallEntity Wall { get; private set; } = null!;
    }
}
