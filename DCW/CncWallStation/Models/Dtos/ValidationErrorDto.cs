using CncWallStation.Models.Enums;

namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 校验错误 DTO
    /// </summary>
    public class ValidationErrorDto
    {
        /// <summary>主键</summary>
        public long Id { get; set; }

        /// <summary>关联墙体ID</summary>
        public long WallId { get; set; }

        /// <summary>分组标识</summary>
        public string GroupId { get; set; } = string.Empty;

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; set; }

        /// <summary>错误码</summary>
        public string? ErrorCode { get; set; }

        /// <summary>错误描述</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; }
    }
}
