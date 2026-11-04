using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.OpcUa
{
    /// <summary>
    /// OPC 节点 Excel 导入导出服务（ClosedXML）
    /// </summary>
    public interface IOpcNodeExcelService
    {
        /// <summary>
        /// 导出节点为 xlsx（列：分组 / NodeId / 描述 / 可写 / 实时显示）
        /// </summary>
        /// <param name="filePath">目标文件路径</param>
        /// <param name="rows">要导出的节点（通常为当前查询结果）</param>
        /// <param name="ungroupedLabel">未分组节点的显示文案</param>
        Task ExportAsync(string filePath, IReadOnlyList<OpcNodeDto> rows, string ungroupedLabel);

        /// <summary>
        /// 解析并校验 xlsx（不写库）。有效行与逐行错误分别返回，供预览弹窗展示。
        /// </summary>
        /// <exception cref="InvalidOperationException">文件无法解析或缺少 NodeId 列</exception>
        Task<OpcNodeImportParseResult> ReadAsync(string filePath);
    }
}
