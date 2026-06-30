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

        /// <summary>异常描述/原因</summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string Description { get; set; } = string.Empty;

        /// <summary>现场照片路径（JSON 数组字符串）</summary>
        [Column(TypeName = "TEXT")]
        public string? PhotoPaths { get; set; }

        /// <summary>操作人</summary>
        [Column(TypeName = "VARCHAR(64)")]
        public string Operator { get; set; } = string.Empty;

        /// <summary>异常发生时间</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>是否已解决</summary>
        public bool IsResolved { get; set; }

        protected MachiningExceptionEntity() { }

        public MachiningExceptionEntity(long wallId, int exceptionType, string description, string operatorName,
            string? customType = null, string? photoPaths = null)
        {
            WallId = wallId;
            ExceptionType = exceptionType;
            Description = description;
            Operator = operatorName;
            CustomType = customType;
            PhotoPaths = photoPaths;
            CreatedAt = DateTime.Now;
            IsResolved = false;
        }
    }
}
