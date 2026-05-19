namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 批量预检汇总 DTO
    /// </summary>
    public class BatchCheckSummaryDto
    {
        /// <summary>筛选条件</summary>
        public WallFilterDto Filter { get; set; } = new();

        /// <summary>预检开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>预检结束时间</summary>
        public DateTime EndTime { get; set; }

        /// <summary>总耗时（毫秒）</summary>
        public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;

        /// <summary>计划预检总数</summary>
        public int TotalCount { get; set; }

        /// <summary>已完成预检数</summary>
        public int CompletedCount { get; set; }

        /// <summary>发现异常总数</summary>
        public int TotalErrors { get; set; }

        /// <summary>通过数</summary>
        public int PassedCount { get; set; }

        /// <summary>失败数</summary>
        public int FailedCount { get; set; }

        /// <summary>各墙体预检结果（按严重程度排序，问题越多越靠前）</summary>
        public List<DataCheckResultDto> WallResults { get; set; } = new();

        /// <summary>问题墙体 Top 榜（按 CriticalCount 降序）</summary>
        public List<DataCheckResultDto> TopProblemWalls => WallResults
            .OrderByDescending(r => r.CriticalCount)
            .ThenByDescending(r => r.TotalErrorCount)
            .Take(20)
            .ToList();

        /// <summary>操作人</summary>
        public string Operator { get; set; } = string.Empty;
    }
}
