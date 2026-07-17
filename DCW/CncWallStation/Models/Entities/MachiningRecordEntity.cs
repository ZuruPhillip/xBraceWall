using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 加工记录实体（归档）
    /// </summary>
    [Table("MachiningRecord")]
    public class MachiningRecordEntity : Entity<long>
    {
        /// <summary>关联墙体数据库主键</summary>
        public long WallId { get; set; }

        /// <summary>操作人</summary>
        [Column(TypeName = "VARCHAR(64)")]
        public string Operator { get; set; } = string.Empty;

        /// <summary>加工开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>加工结束时间</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>加工总耗时（秒数）</summary>
        public long? TotalDurationSeconds { get; set; }

        /// <summary>加工完成时的状态</summary>
        public int Status { get; set; }

        protected MachiningRecordEntity() { }

        public MachiningRecordEntity(long wallId, string operatorName, DateTime startTime, DateTime? endTime = null,
            long? totalDurationSeconds = null, int status = 0)
        {
            WallId = wallId;
            Operator = operatorName;
            StartTime = startTime;
            EndTime = endTime;
            TotalDurationSeconds = totalDurationSeconds;
            Status = status;
        }
    }
}
