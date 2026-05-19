using CncWallStation.Models.Enums;
using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 数据预检记录主表 — 持久化每次预检事件的元数据
    /// </summary>
    [Table("DataCheckRecord")]
    public class DataCheckRecordEntity : Entity<string>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected DataCheckRecordEntity() { }

        public DataCheckRecordEntity(
            string groupId,
            long wallId,
            string version,
            double bimScore,
            double momScore,
            int errorCount,
            int criticalCount,
            string @operator,
            CheckResult result,
            long durationMs)
        {
            Id = groupId;  // GroupId 即主键
            WallId = wallId;
            Version = version;
            BimScore = bimScore;
            MomScore = momScore;
            ErrorCount = errorCount;
            CriticalCount = criticalCount;
            Operator = @operator;
            CheckTime = DateTime.Now;
            DurationMs = durationMs;
            Result = result;
        }

        // ==================== 属性 ====================

        /// <summary>关联墙体（外键 → Wall.Id）</summary>
        [Required]
        public long WallId { get; private set; }

        /// <summary>数据版本号</summary>
        [Required]
        [MaxLength(32)]
        public string Version { get; private set; } = string.Empty;

        /// <summary>BimData 总分</summary>
        public double BimScore { get; private set; }

        /// <summary>MomData 总分</summary>
        public double MomScore { get; private set; }

        /// <summary>异常总条数</summary>
        public int ErrorCount { get; private set; }

        /// <summary>致命异常数</summary>
        public int CriticalCount { get; private set; }

        /// <summary>操作人</summary>
        [Required]
        [MaxLength(128)]
        public string Operator { get; private set; } = string.Empty;

        /// <summary>预检时间</summary>
        [Required]
        public DateTime CheckTime { get; private set; } = DateTime.Now;

        /// <summary>预检耗时（毫秒）</summary>
        public long DurationMs { get; private set; }

        /// <summary>预检结果：Pass / Fail / Override</summary>
        public CheckResult Result { get; private set; }

        // ==================== 导航属性 ====================

        /// <summary>关联的墙体</summary>
        [ForeignKey(nameof(WallId))]
        public WallEntity Wall { get; private set; } = null!;

        /// <summary>本次预检产生的所有异常明细</summary>
        public ICollection<ValidationErrorEntity> Errors { get; private set; } = new List<ValidationErrorEntity>();
    }
}
