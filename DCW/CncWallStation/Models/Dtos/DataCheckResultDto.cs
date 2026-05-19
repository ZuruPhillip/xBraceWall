using CncWallStation.Models.Entities;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 数据预检结果汇总 DTO
    /// </summary>
    public class DataCheckResultDto
    {
        /// <summary>预检分组ID（Guid）</summary>
        public string GroupId { get; set; } = string.Empty;

        /// <summary>墙体ID（数据库主键）</summary>
        public long WallId { get; set; }

        /// <summary>墙体业务标识</summary>
        public string WallKey { get; set; } = string.Empty;

        /// <summary>数据版本号</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>BimData 特征校验结果</summary>
        public List<FeatureCategoryResult> BimFeatureResults { get; set; } = new();

        /// <summary>MomData 特征校验结果</summary>
        public List<FeatureCategoryResult> MomFeatureResults { get; set; } = new();

        /// <summary>BimData 总分</summary>
        public double BimTotalScore { get; set; }

        /// <summary>MomData 总分</summary>
        public double MomTotalScore { get; set; }

        /// <summary>致命异常数</summary>
        public int CriticalCount { get; set; }

        /// <summary>错误异常数</summary>
        public int ErrorCount { get; set; }

        /// <summary>警告异常数</summary>
        public int WarningCount { get; set; }

        /// <summary>提示异常数</summary>
        public int InfoCount { get; set; }

        /// <summary>总异常数</summary>
        public int TotalErrorCount => CriticalCount + ErrorCount + WarningCount + InfoCount;

        /// <summary>所有异常明细</summary>
        public List<ValidationErrorEntity> AllErrors { get; set; } = new();

        /// <summary>预检耗时（毫秒）</summary>
        public long DurationMs { get; set; }

        /// <summary>操作人</summary>
        public string Operator { get; set; } = string.Empty;

        /// <summary>是否整体通过</summary>
        public bool IsPassed => CriticalCount == 0 && BimTotalScore >= 60 && MomTotalScore >= 60;
    }
}
