using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// 数据预检服务接口
    /// </summary>
    public interface IDataCheckService
    {
        /// <summary>单墙预检</summary>
        Task<DataCheckResultDto> CheckSingleWallAsync(long wallId, string @operator);

        /// <summary>批量预检（带进度回调）</summary>
        Task<BatchCheckSummaryDto> CheckBatchAsync(
            WallFilterDto filter,
            string @operator,
            IProgress<(int Done, int Total, int Errors)>? progress = null);

        /// <summary>获取墙体历史预检记录</summary>
        Task<List<DataCheckRecordDto>> GetHistoryAsync(long wallId);

        /// <summary>对比两次预检结果</summary>
        Task<HistoryDiffResultDto> CompareAsync(string groupId1, string groupId2);
    }

    /// <summary>
    /// 预检记录 DTO（用于历史列表展示）
    /// </summary>
    public class DataCheckRecordDto
    {
        public string GroupId { get; set; } = string.Empty;
        public long WallId { get; set; }
        public string WallKey { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public double BimScore { get; set; }
        public double MomScore { get; set; }
        public int ErrorCount { get; set; }
        public int CriticalCount { get; set; }
        public string Operator { get; set; } = string.Empty;
        public DateTime CheckTime { get; set; }
        public long DurationMs { get; set; }
        public string ResultText { get; set; } = string.Empty;
    }
}
