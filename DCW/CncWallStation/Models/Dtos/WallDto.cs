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

        /// <summary>项目名称</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>墙体唯一标识</summary>
        public string WallId { get; set; } = string.Empty;

        /// <summary>墙体名称</summary>
        public string WallName { get; set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; set; }

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; set; }

        /// <summary>管线阶段显示文本（支持中英文）</summary>
        public string PipelineStageText =>
            CncWallStation.Localization.LocalizationService.Instance.CurrentLanguage.StartsWith("en")
                ? PipelineStage.ToDisplayTextEn()
                : PipelineStage.ToDisplayText();

        /// <summary>加工优先级（int）</summary>
        public int Priority { get; set; }

        /// <summary>生产状态</summary>
        public int Status { get; set; }

        /// <summary>审核状态：0=未审核，1=已审核</summary>
        public int AuditStatus { get; set; }

        /// <summary>Schema 版本号（来自 BimJson）</summary>
        public string SchemaVersion { get; set; } = "V0.0.0";

		/// <summary>开始生产时间</summary>
		public DateTime? StartProductionTime { get; set; }

		/// <summary>结束生产时间</summary>
		public DateTime? EndProductionTime { get; set; }

		/// <summary>导入时间</summary>
		public DateTime ImportTime { get; set; }

		/// <summary>最后更新时间</summary>
		public DateTime UpdatedAt { get; set; }

        /// <summary>最后修改人</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>校验失败原因摘要</summary>
        public string? ValidationErrorSummary { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }
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
