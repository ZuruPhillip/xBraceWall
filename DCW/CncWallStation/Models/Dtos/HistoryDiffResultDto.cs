using CncWallStation.Models.Entities;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 差异对比的单条异常条目
    /// </summary>
    public class DiffErrorEntry
    {
        /// <summary>错误码</summary>
        public string? ErrorCode { get; set; }

        /// <summary>特征类别</summary>
        public string? FeatureCategory { get; set; }

        /// <summary>中文描述</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>英文描述</summary>
        public string? ErrorMessageEn { get; set; }

        /// <summary>严重等级</summary>
        public string SeverityText { get; set; } = string.Empty;
    }

    /// <summary>
    /// 历史对比差异结果 DTO（两次预检对比）
    /// </summary>
    public class HistoryDiffResultDto
    {
        /// <summary>第一次预检记录（较早）</summary>
        public DataCheckRecordEntity? Record1 { get; set; }

        /// <summary>第二次预检记录（较晚）</summary>
        public DataCheckRecordEntity? Record2 { get; set; }

        /// <summary>新增异常（Record2 有，Record1 没有）</summary>
        public List<DiffErrorEntry> NewErrors { get; set; } = new();

        /// <summary>已修复异常（Record1 有，Record2 没有）</summary>
        public List<DiffErrorEntry> FixedErrors { get; set; } = new();

        /// <summary>仍存在异常（两次都有）</summary>
        public List<DiffErrorEntry> PersistentErrors { get; set; } = new();
    }
}
