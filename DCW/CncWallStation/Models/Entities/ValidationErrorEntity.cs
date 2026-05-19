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

        /// <summary>
        /// 创建校验错误记录（向后兼容构造器，用于 PipelineService）
        /// </summary>
        public ValidationErrorEntity(
            long wallId,
            string groupId,
            PipelineStage pipelineStage,
            string errorMessage,
            string? errorCode = null)
            : this(
                  wallId,
                  groupId,
                  pipelineStage,
                  errorMessage,
                  errorCode,
                  ErrorSeverity.Error,
                  ErrorCategory.Bim,
                  null,
                  null,
                  null)
        {
        }

        /// <summary>
        /// 创建校验错误记录（完整构造器，用于 DataCheckService）
        /// </summary>
        public ValidationErrorEntity(
            long? wallId,
            string groupId,
            PipelineStage pipelineStage,
            string errorMessage,
            string? errorCode,
            ErrorSeverity severity,
            ErrorCategory errorCategory,
            string? featureCategory,
            string? errorMessageEn,
            string? dataCheckGroupId)
        {
            WallId = wallId;
            GroupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            PipelineStage = pipelineStage;
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            ErrorCode = errorCode;
            Severity = severity;
            ErrorCategory = errorCategory;
            FeatureCategory = featureCategory;
            ErrorMessageEn = errorMessageEn;
            DataCheckGroupId = dataCheckGroupId;
            CreatedAt = DateTime.Now;
        }

        // ==================== 属性 ====================

        /// <summary>关联墙体（外键 → Wall.Id），改为可空以支持通过 DataCheckRecord 关联</summary>
        public long? WallId { get; private set; }

        /// <summary>同一次校验/转换操作的分组标识（Guid），用于生成错误报告</summary>
        [Required]
        [MaxLength(64)]
        public string GroupId { get; private set; } = string.Empty;

        /// <summary>关联预检记录（外键 → DataCheckRecord.Id）</summary>
        [MaxLength(64)]
        public string? DataCheckGroupId { get; private set; }

        /// <summary>失败时所处的管线阶段</summary>
        public PipelineStage PipelineStage { get; private set; }

        /// <summary>错误码（便于分类统计）</summary>
        [MaxLength(128)]
        public string? ErrorCode { get; private set; }

        /// <summary>失败原因详情（中文）</summary>
        [Required]
        [Column(TypeName = "text")]
        public string ErrorMessage { get; private set; } = string.Empty;

        /// <summary>失败原因详情（英文）</summary>
        [Column(TypeName = "text")]
        public string? ErrorMessageEn { get; private set; }

        /// <summary>严重等级</summary>
        public ErrorSeverity Severity { get; private set; } = ErrorSeverity.Error;

        /// <summary>错误分类：BimError / MomError</summary>
        public ErrorCategory ErrorCategory { get; private set; } = ErrorCategory.Bim;

        /// <summary>特征类别名称（如 aacWallElevation, steelFrameColumns）</summary>
        [MaxLength(128)]
        public string? FeatureCategory { get; private set; }

        /// <summary>创建时间</summary>
        [Required]
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        // ==================== 领域方法 ====================

        /// <summary>
        /// 绑定到预检记录（DataCheckService 调用）
        /// </summary>
        public void BindToCheckRecord(string groupId, long wallId)
        {
            GroupId = groupId;
            DataCheckGroupId = groupId;
            WallId = wallId;
        }

        // ==================== 导航属性 ====================

        /// <summary>关联的墙体</summary>
        [ForeignKey(nameof(WallId))]
        public WallEntity? Wall { get; private set; }

        /// <summary>关联的预检记录</summary>
        [ForeignKey(nameof(DataCheckGroupId))]
        public DataCheckRecordEntity? DataCheckRecord { get; private set; }
    }
}
