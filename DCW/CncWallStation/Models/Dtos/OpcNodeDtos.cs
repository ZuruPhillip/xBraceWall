namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// OPC 节点传输对象
    /// </summary>
    public class OpcNodeDto
    {
        public long Id { get; set; }

        /// <summary>所属分组（null 表示未分组）</summary>
        public int? GroupId { get; set; }

        /// <summary>所属分组名称（未分组时为 null）</summary>
        public string? GroupName { get; set; }

        public string NodeId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsWritable { get; set; }

        public bool IsShowInRealtime { get; set; }
    }

    /// <summary>
    /// OPC 节点分组传输对象
    /// </summary>
    public class OpcNodeGroupDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>该分组下的节点数量</summary>
        public int NodeCount { get; set; }
    }

    /// <summary>
    /// Excel 导入的单行数据（已通过格式校验）
    /// </summary>
    public class OpcNodeImportRow
    {
        /// <summary>Excel 中的行号（1 基，含表头计入），用于错误提示</summary>
        public int RowNumber { get; set; }

        /// <summary>分组名称（为空表示未分组；不存在时自动创建）</summary>
        public string? GroupName { get; set; }

        public string NodeId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsWritable { get; set; }

        public bool IsShowInRealtime { get; set; }
    }

    /// <summary>
    /// Excel 解析结果：有效行 + 校验失败行
    /// </summary>
    public class OpcNodeImportParseResult
    {
        public List<OpcNodeImportRow> Rows { get; } = new();

        /// <summary>校验失败信息（"第 N 行：原因"）</summary>
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Excel 导入执行结果
    /// </summary>
    public class OpcNodeImportResult
    {
        public int AddedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int SkippedCount { get; set; }

        public List<string> Errors { get; } = new();
    }
}
