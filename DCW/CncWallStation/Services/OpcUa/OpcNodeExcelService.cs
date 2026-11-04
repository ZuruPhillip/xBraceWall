using ClosedXML.Excel;
using CncWallStation.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.OpcUa
{
    /// <summary>
    /// OPC 节点 Excel 导入导出实现（ClosedXML）
    /// </summary>
    public class OpcNodeExcelService : IOpcNodeExcelService
    {
        /// <summary>NodeId 最大长度（与表结构一致）</summary>
        private const int MaxNodeIdLength = 256;

        /// <summary>分组名称最大长度</summary>
        private const int MaxGroupNameLength = 64;

        /// <summary>表头（中英双语，导入时按关键字匹配）</summary>
        private static readonly string[] Headers = { "分组 / Group", "NodeId", "描述 / Description", "可写 / Writable", "实时显示 / Realtime" };

        private readonly ILogger<OpcNodeExcelService> _logger;

        public OpcNodeExcelService(ILogger<OpcNodeExcelService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task ExportAsync(string filePath, IReadOnlyList<OpcNodeDto> rows, string ungroupedLabel)
        {
            return Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("OPC节点");

                for (var i = 0; i < Headers.Length; i++)
                    sheet.Cell(1, i + 1).Value = Headers[i];

                var headerRange = sheet.Range(1, 1, 1, Headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4FF");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var rowIndex = 2;
                foreach (var node in rows)
                {
                    sheet.Cell(rowIndex, 1).Value = string.IsNullOrWhiteSpace(node.GroupName)
                        ? ungroupedLabel
                        : node.GroupName;
                    sheet.Cell(rowIndex, 2).Value = node.NodeId;
                    sheet.Cell(rowIndex, 3).Value = node.Description;
                    sheet.Cell(rowIndex, 4).Value = node.IsWritable ? "是" : "否";
                    sheet.Cell(rowIndex, 5).Value = node.IsShowInRealtime ? "是" : "否";
                    rowIndex++;
                }

                sheet.SheetView.FreezeRows(1);
                sheet.Column(1).Width = 18;
                sheet.Column(2).Width = 46;
                sheet.Column(3).Width = 26;
                sheet.Column(4).Width = 12;
                sheet.Column(5).Width = 14;

                workbook.SaveAs(filePath);

                _logger.LogInformation("已导出 {Count} 个 OPC 节点到 {File}", rows.Count, filePath);
            });
        }

        /// <inheritdoc/>
        public Task<OpcNodeImportParseResult> ReadAsync(string filePath)
        {
            return Task.Run(() => Parse(filePath));
        }

        private OpcNodeImportParseResult Parse(string filePath)
        {
            var result = new OpcNodeImportParseResult();

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "打开 Excel 文件失败: {File}", filePath);
                throw new InvalidOperationException($"无法读取 Excel 文件：{ex.Message}", ex);
            }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("Excel 文件中没有工作表");

                var headerRowNumber = FindHeaderRow(sheet);
                if (headerRowNumber <= 0)
                    throw new InvalidOperationException("未找到表头行，请使用导出的模板进行导入");

                var (colGroup, colNodeId, colDescription, colWritable, colRealtime) =
                    MapColumns(sheet, headerRowNumber);

                if (colNodeId <= 0)
                    throw new InvalidOperationException("未找到 NodeId 列，请使用导出的模板进行导入");

                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
                var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
                {
                    var row = sheet.Row(rowNumber);

                    var nodeId = GetCellText(row, colNodeId);
                    var groupName = GetCellText(row, colGroup);
                    var description = GetCellText(row, colDescription);
                    var writableText = GetCellText(row, colWritable);
                    var realtimeText = GetCellText(row, colRealtime);

                    var isBlankRow = nodeId.Length == 0 && groupName.Length == 0
                                     && description.Length == 0
                                     && writableText.Length == 0 && realtimeText.Length == 0;
                    if (isBlankRow)
                        continue;

                    if (nodeId.Length == 0)
                    {
                        result.Errors.Add($"第 {rowNumber} 行：NodeId 为空，已跳过");
                        continue;
                    }

                    if (nodeId.Length > MaxNodeIdLength)
                    {
                        result.Errors.Add($"第 {rowNumber} 行：NodeId 长度超过 {MaxNodeIdLength}，已跳过");
                        continue;
                    }

                    if (seen.TryGetValue(nodeId, out var firstRow))
                    {
                        result.Errors.Add($"第 {rowNumber} 行：NodeId 与第 {firstRow} 行重复，已跳过");
                        continue;
                    }

                    if (!TryParseBool(writableText, out var isWritable))
                    {
                        result.Errors.Add($"第 {rowNumber} 行：“可写”列的值 “{writableText}” 无法识别，已跳过");
                        continue;
                    }

                    if (!TryParseBool(realtimeText, out var isShowInRealtime))
                    {
                        result.Errors.Add($"第 {rowNumber} 行：“实时显示”列的值 “{realtimeText}” 无法识别，已跳过");
                        continue;
                    }

                    if (groupName.Length > MaxGroupNameLength)
                        groupName = groupName[..MaxGroupNameLength];

                    seen[nodeId] = rowNumber;
                    result.Rows.Add(new OpcNodeImportRow
                    {
                        RowNumber = rowNumber,
                        NodeId = nodeId,
                        GroupName = groupName.Length == 0 ? null : groupName,
                        Description = description,
                        IsWritable = isWritable,
                        IsShowInRealtime = isShowInRealtime
                    });
                }

                _logger.LogInformation(
                    "Excel 解析完成: {File}，有效 {Valid} 行，异常 {Invalid} 行",
                    filePath, result.Rows.Count, result.Errors.Count);
            }

            return result;
        }

        /// <summary>查找表头行（前 10 行内首个包含 NodeId 的行）</summary>
        private static int FindHeaderRow(IXLWorksheet sheet)
        {
            var limit = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 10);

            for (var rowNumber = 1; rowNumber <= limit; rowNumber++)
            {
                foreach (var cell in sheet.Row(rowNumber).CellsUsed())
                {
                    if (Normalize(cell.GetFormattedString()).Contains("nodeid"))
                        return rowNumber;
                }
            }

            return -1;
        }

        /// <summary>按表头文字匹配列（兼容中英文表头）</summary>
        private static (int Group, int NodeId, int Description, int Writable, int Realtime) MapColumns(
            IXLWorksheet sheet, int headerRowNumber)
        {
            var colGroup = -1;
            var colNodeId = -1;
            var colDescription = -1;
            var colWritable = -1;
            var colRealtime = -1;

            foreach (var cell in sheet.Row(headerRowNumber).CellsUsed())
            {
                var header = Normalize(cell.GetFormattedString());
                var index = cell.Address.ColumnNumber;

                if (header.Contains("nodeid"))
                    colNodeId = index;
                else if (header.Contains("分组") || header.Contains("group"))
                    colGroup = index;
                else if (header.Contains("描述") || header.Contains("desc"))
                    colDescription = index;
                else if (header.Contains("可写") || header.Contains("writ"))
                    colWritable = index;
                else if (header.Contains("实时") || header.Contains("realtime"))
                    colRealtime = index;
            }

            return (colGroup, colNodeId, colDescription, colWritable, colRealtime);
        }

        private static string GetCellText(IXLRow row, int column)
        {
            if (column <= 0)
                return string.Empty;

            var cell = row.Cell(column);
            return cell.IsEmpty() ? string.Empty : cell.GetFormattedString().Trim();
        }

        /// <summary>去掉空白与常见分隔符并转小写，便于表头匹配</summary>
        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Replace(" ", string.Empty)
                        .Replace("/", string.Empty)
                        .Replace("\t", string.Empty)
                        .Trim()
                        .ToLowerInvariant();
        }

        /// <summary>布尔列解析：空视为 false，兼容 是/否、Y/N、1/0、true/false</summary>
        private static bool TryParseBool(string? text, out bool value)
        {
            value = false;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            var normalized = text.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "是":
                case "y":
                case "yes":
                case "true":
                case "1":
                case "√":
                    value = true;
                    return true;

                case "否":
                case "n":
                case "no":
                case "false":
                case "0":
                case "×":
                case "-":
                    value = false;
                    return true;

                default:
                    // 兼容 “是(Y)” 这类写法
                    if (normalized.StartsWith('是') || normalized.StartsWith('y') || normalized.StartsWith('t'))
                    {
                        value = true;
                        return true;
                    }

                    if (normalized.StartsWith('否') || normalized.StartsWith('n') || normalized.StartsWith('f'))
                    {
                        value = false;
                        return true;
                    }

                    return false;
            }
        }
    }
}
