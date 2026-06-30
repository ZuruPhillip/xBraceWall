using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 加工异常报告实体
    /// </summary>
    [Table("MachiningException")]
    public class MachiningExceptionEntity : Entity<long>
    {
        /// <summary>关联墙体数据库主键</summary>
        public long WallId { get; set; }

        /// <summary>异常类型（对应 ExceptionType 枚举值）</summary>
        public int ExceptionType { get; set; }

        /// <summary>自定义异常类型（当 ExceptionType=其他时填写）</summary>
        [Column(TypeName = "VARCHAR(128)")]
        public string? CustomType { get; set; }

        /// <summary>故障描述</summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string Description { get; set; } = string.Empty;

        /// <summary>现场照片路径（JSON 数组字符串）</summary>
        [Column(TypeName = "TEXT")]
        public string? PhotoPaths { get; set; }

        /// <summary>登记人</summary>
        [Column(TypeName = "VARCHAR(64)")]
        public string Registrant { get; set; } = string.Empty;

        /// <summary>登记时间（系统自动记录）</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>异常发生时间（用户输入）</summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>故障频次</summary>
        public int FrequencyCount { get; set; }

        /// <summary>是否已解决</summary>
        public bool IsResolved { get; set; }

        /// <summary>维修方法</summary>
        [Column(TypeName = "VARCHAR(512)")]
        public string? RepairMethod { get; set; }

        /// <summary>解决人员</summary>
        [Column(TypeName = "VARCHAR(64)")]
        public string? Resolver { get; set; }

        /// <summary>维修耗时（小时，数字）</summary>
        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal? RepairDuration { get; set; }

        /// <summary>完成时间</summary>
        public DateTime? CompletionTime { get; set; }

        /// <summary>机构改善建议</summary>
        [Column(TypeName = "TEXT")]
        public string? ImprovementSuggestion { get; set; }

        /// <summary>备注</summary>
        [Column(TypeName = "TEXT")]
        public string? Remarks { get; set; }

        protected MachiningExceptionEntity() { }

        public MachiningExceptionEntity(long wallId, int exceptionType, string description, string registrant,
            DateTime occurredAt, int frequencyCount,
            string? customType = null, string? photoPaths = null)
        {
            WallId = wallId;
            ExceptionType = exceptionType;
            Description = description;
            Registrant = registrant;
            OccurredAt = occurredAt;
            FrequencyCount = frequencyCount;
            CustomType = customType;
            PhotoPaths = photoPaths;
            CreatedAt = DateTime.Now;
            IsResolved = false;
        }
    }
}
