using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 单特征类别校验结果
    /// </summary>
    public class FeatureCategoryResult
    {
        /// <summary>特征类别名称（英文标识）</summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>特征类别中文名称</summary>
        public string CategoryNameCn { get; set; } = string.Empty;

        /// <summary>检查项总数</summary>
        public int CheckItemCount { get; set; }

        /// <summary>致命异常数</summary>
        public int CriticalCount { get; set; }

        /// <summary>错误异常数</summary>
        public int ErrorCount { get; set; }

        /// <summary>警告异常数</summary>
        public int WarningCount { get; set; }

        /// <summary>提示异常数</summary>
        public int InfoCount { get; set; }

        /// <summary>该特征类别得分（0-100）</summary>
        public double Score { get; set; }

        /// <summary>该特征下的详细异常列表</summary>
        public List<ValidationErrorEntity> Errors { get; set; } = new();

        /// <summary>是否通过（无 Critical 且 Score >= 阈值）</summary>
        public bool IsPassed => CriticalCount == 0 && Score >= 60;
    }
}
