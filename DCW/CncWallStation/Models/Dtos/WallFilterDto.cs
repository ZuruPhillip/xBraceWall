using CncWallStation.Models.Enums;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 批量预检筛选条件
    /// </summary>
    public class WallFilterDto
    {
        /// <summary>项目名称（可为空，表示不限）</summary>
        public string? ProjectName { get; set; }

        /// <summary>楼层（可为空，表示不限）</summary>
        public int? Floor { get; set; }

        /// <summary>开始时间（可为空）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>结束时间（可为空）</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>管线阶段筛选（多选，为空表示不限）</summary>
        public List<PipelineStage>? PipelineStages { get; set; }

        /// <summary>最大预检数量（0 表示不限制）</summary>
        public int MaxCount { get; set; } = 0;

	}
}
