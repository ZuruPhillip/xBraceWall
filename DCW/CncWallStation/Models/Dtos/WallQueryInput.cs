namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 墙体查询筛选条件 DTO
    /// </summary>
    public class WallQueryInput
    {
        /// <summary>项目名称（模糊搜索）</summary>
        public string? ProjectName { get; set; }

        /// <summary>楼层</summary>
        public int? Floor { get; set; }

        /// <summary>墙体ID（模糊搜索）</summary>
        public string? WallId { get; set; }

        /// <summary>墙体名称（模糊搜索）</summary>
        public string? WallName { get; set; }

        /// <summary>生产状态列表</summary>
        public List<int>? Statuses { get; set; }

        /// <summary>优先级列表</summary>
        public List<int>? Priorities { get; set; }

        /// <summary>管线阶段列表</summary>
        public List<Enums.PipelineStage>? PipelineStages { get; set; }

        /// <summary>审核状态列表</summary>
        public List<int>? AuditStatuses { get; set; }

		/// <summary>生产结束时间起</summary>
		public DateTime? EndProductionTimeFrom { get; set; }

		/// <summary>生产结束时间止</summary>
		public DateTime? EndProductionTimeTo { get; set; }

        /// <summary>排序字段</summary>
        public string? SortField { get; set; }

        /// <summary>是否升序</summary>
        public bool SortAscending { get; set; } = true;

        /// <summary>页码（从1开始）</summary>
        public int Page { get; set; } = 1;

        /// <summary>每页条数</summary>
        public int PageSize { get; set; } = 20;

        /// <summary>仅查询最新版本</summary>
        public bool LatestOnly { get; set; } = true;

        /// <summary>是否包含已删除数据（默认 false）</summary>
        public bool IncludeDeleted { get; set; } = false;
    }
}
