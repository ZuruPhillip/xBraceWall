using CncWallStation.Models.Enums;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 墙体列表项 DTO（轻量，不含大字段）
    /// </summary>
    public class WallDto
    {
        /// <summary>主键</summary>
        public long Id { get; set; }

        /// <summary>项目号</summary>
        public string ProjectNumber { get; set; } = string.Empty;

        /// <summary>墙体唯一标识</summary>
        public string WallId { get; set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; set; }

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; set; }

        /// <summary>管线阶段中文显示文本</summary>
        public string PipelineStageText => PipelineStage.ToDisplayText();

        /// <summary>加工优先级</summary>
        public int Priority { get; set; }

        /// <summary>加工状态</summary>
        public int Status { get; set; }

        /// <summary>版本号</summary>
        public int Version { get; set; }

        /// <summary>导入时间</summary>
        public DateTime ImportTime { get; set; }

        /// <summary>最后更新时间</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>最后修改人</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>校验失败原因摘要</summary>
        public string? ValidationErrorSummary { get; set; }
    }

    /// <summary>
    /// 墙体详情 DTO（含完整数据）
    /// </summary>
    public class WallDetailDto : WallDto
    {
        /// <summary>完整 BimJSON</summary>
        public string BimJsonData { get; set; } = string.Empty;

        /// <summary>完整 MomJSON</summary>
        public string? MomJsonData { get; set; }

        /// <summary>校验错误详情列表</summary>
        public List<ValidationErrorDto> ValidationErrors { get; set; } = new();
    }
}
