namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// 项目批次 DTO
    /// </summary>
    public class ProjectDto
    {
        /// <summary>主键</summary>
        public int Id { get; set; }

        /// <summary>项目号</summary>
        public string ProjectNumber { get; set; } = string.Empty;

        /// <summary>版本号</summary>
        public int Version { get; set; }

        /// <summary>是否最新版本</summary>
        public bool IsLatest { get; set; }

        /// <summary>源文件夹路径</summary>
        public string? SourceFolderPath { get; set; }

        /// <summary>导入主机名</summary>
        public string? HostName { get; set; }

        /// <summary>导入者</summary>
        public string? ImportedBy { get; set; }

        /// <summary>墙体总数</summary>
        public int TotalWalls { get; set; }

        /// <summary>导入时间</summary>
        public DateTime ImportTime { get; set; }

        /// <summary>备注</summary>
        public string? Notes { get; set; }
    }
}
