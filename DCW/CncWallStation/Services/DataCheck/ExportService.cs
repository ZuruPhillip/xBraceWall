using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// 导出服务 — PDF 报告 + Excel 明细
    /// </summary>
    public class ExportService
    {
        private readonly ILogger<ExportService> _logger;

        // 中文字体名称（默认为微软雅黑，WPF 环境中通常已安装）
        private const string FontFamily = "Microsoft YaHei";

        // 颜色定义
        private static readonly XColor ColorCritical = XColor.FromArgb(255, 0xFF, 0x4D, 0x4F);
        private static readonly XColor ColorError = XColor.FromArgb(255, 0xFF, 0x7A, 0x45);
        private static readonly XColor ColorWarning = XColor.FromArgb(255, 0xFA, 0xAD, 0x14);
        private static readonly XColor ColorInfo = XColor.FromArgb(255, 0x16, 0x77, 0xFF);
        private static readonly XColor ColorPass = XColor.FromArgb(255, 0x52, 0xC4, 0x1A);
        private static readonly XColor ColorDark = XColor.FromArgb(255, 0x33, 0x33, 0x33);
        private static readonly XColor ColorGray = XColor.FromArgb(255, 0x99, 0x99, 0x99);
        private static readonly XColor ColorLightBg = XColor.FromArgb(255, 0xF5, 0xF5, 0xF5);

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        // ==================== PDF 报告导出 ====================

        public async Task ExportPdfAsync(DataCheckResultDto result, string filePath)
        {
            using var doc = new PdfDocument();
            doc.Info.Title = $"数据预检报告 - {result.WallKey}";
            doc.Info.Author = result.Operator;

            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(297);
            page.Height = XUnit.FromMillimeter(210);
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont(FontFamily, 10, XFontStyle.Regular);
            var fontBold = new XFont(FontFamily, 14, XFontStyle.Bold);
            var fontHeader = new XFont(FontFamily, 18, XFontStyle.Bold);
            var fontSmall = new XFont(FontFamily, 8, XFontStyle.Regular);

            float y = 30;
            float leftMargin = 30;
            float rightMargin = 270;

            // ── 标题 ──
            gfx.DrawString("数据预检报告", fontHeader, XBrushes.DarkRed,
                new XRect(leftMargin, y, rightMargin - leftMargin, 25), XStringFormats.TopLeft);
            y += 30;

            // ── 分隔线 ──
            gfx.DrawLine(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1),
                leftMargin, y, rightMargin, y);
            y += 15;

            // ── 墙体信息表格 ──
            gfx.DrawString("墙体信息", fontBold, XBrushes.Black,
                new XRect(leftMargin, y, rightMargin - leftMargin, 20), XStringFormats.TopLeft);
            y += 22;

            DrawInfoRow(gfx, ref y, leftMargin, rightMargin, "墙体ID", result.WallKey, font, fontSmall);
            DrawInfoRow(gfx, ref y, leftMargin, rightMargin, "数据版本", result.Version, font, fontSmall);
            DrawInfoRow(gfx, ref y, leftMargin, rightMargin, "GroupId", result.GroupId, font, fontSmall);
            DrawInfoRow(gfx, ref y, leftMargin, rightMargin, "操作员", result.Operator, font, fontSmall);
            DrawInfoRow(gfx, ref y, leftMargin, rightMargin, "预检耗时", $"{result.DurationMs}ms", font, fontSmall);

            y += 10;
            gfx.DrawLine(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1),
                leftMargin, y, rightMargin, y);
            y += 15;

            // ── 评分汇总 ──
            gfx.DrawString("评分汇总", fontBold, XBrushes.Black,
                new XRect(leftMargin, y, rightMargin - leftMargin, 20), XStringFormats.TopLeft);
            y += 25;

            // BimScore card
            DrawScoreCard(gfx, leftMargin, ref y, "BimData 总分", result.BimTotalScore);
            // MomScore card — 回退 y 偏移使其与 Bim 卡片并排
            float momY = y + 35;
            DrawScoreCard(gfx, leftMargin + 90, ref momY, "MomData 总分", result.MomTotalScore);

            y += 40;
            gfx.DrawLine(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1),
                leftMargin, y, rightMargin, y);
            y += 15;

            // ── 严重等级分布 ──
            gfx.DrawString("严重等级分布", fontBold, XBrushes.Black,
                new XRect(leftMargin, y, rightMargin - leftMargin, 20), XStringFormats.TopLeft);
            y += 22;

            DrawSeverityBar(gfx, ref y, leftMargin, rightMargin, "Critical(致命)", result.CriticalCount, ColorCritical, font);
            DrawSeverityBar(gfx, ref y, leftMargin, rightMargin, "Error(错误)", result.ErrorCount, ColorError, font);
            DrawSeverityBar(gfx, ref y, leftMargin, rightMargin, "Warning(警告)", result.WarningCount, ColorWarning, font);
            DrawSeverityBar(gfx, ref y, leftMargin, rightMargin, "Info(提示)", result.InfoCount, ColorInfo, font);

            y += 10;
            gfx.DrawLine(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1),
                leftMargin, y, rightMargin, y);
            y += 15;

            // ── 特征分类统计（BimData） ──
            if (result.BimFeatureResults.Count > 0)
            {
                y = DrawFeatureTable(gfx, y, leftMargin, rightMargin,
                    "BimData 特征分类统计", result.BimFeatureResults, font, fontBold, fontSmall);
            }

            // ── 详细异常清单 ──
            if (result.AllErrors.Count > 0)
            {
                y = DrawErrorList(gfx, y, leftMargin, rightMargin,
                    "详细异常清单", result.AllErrors, font, fontBold, fontSmall);
            }

            // ── 页脚 ──
            gfx.DrawString($"报告生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                fontSmall, XBrushes.Gray, new XRect(leftMargin, 190, rightMargin - leftMargin, 10),
                XStringFormats.CenterLeft);

            doc.Save(filePath);
            _logger.LogInformation("PDF 报告已导出：{Path}", filePath);
        }

        // ==================== Excel 导出 ====================

        public async Task ExportExcelAsync(DataCheckResultDto result, string filePath)
        {
            using var workbook = new XLWorkbook();

            // Sheet 1: 评分汇总
            var wsSummary = workbook.Worksheets.Add("评分汇总");
            BuildSummarySheet(wsSummary, result);

            // Sheet 2: 特征分类统计
            var wsFeatures = workbook.Worksheets.Add("特征分类统计");
            BuildFeaturesSheet(wsFeatures, result);

            // Sheet 3: 异常清单
            if (result.AllErrors.Count > 0)
            {
                var wsErrors = workbook.Worksheets.Add("异常清单");
                BuildErrorsSheet(wsErrors, result.AllErrors);
            }

            workbook.SaveAs(filePath);
            _logger.LogInformation("Excel 报告已导出：{Path}", filePath);
        }

        /// <summary>批量预检汇总 Excel 导出</summary>
        public async Task ExportBatchExcelAsync(BatchCheckSummaryDto summary, string filePath)
        {
            using var workbook = new XLWorkbook();

            var wsSummary = workbook.Worksheets.Add("批量预检汇总");
            BuildBatchSummarySheet(wsSummary, summary);

            workbook.SaveAs(filePath);
            _logger.LogInformation("批量预检 Excel 已导出：{Path}", filePath);
        }

        // ==================== PDF 内部方法 ====================

        private static void DrawInfoRow(XGraphics gfx, ref float y, float left, float right,
            string label, string value, XFont font, XFont fontSmall)
        {
            gfx.DrawString(label, font, XBrushes.Gray,
                new XRect(left, y, 100, 16), XStringFormats.TopLeft);
            gfx.DrawString(value, font, XBrushes.Black,
                new XRect(left + 100, y, right - left - 100, 16), XStringFormats.TopLeft);
            y += 18;
        }

        private static void DrawScoreCard(XGraphics gfx, float x, ref float y, string label, double score)
        {
            var brush = score >= 80 ? XBrushes.Green :
                        score >= 60 ? new XSolidBrush(ColorWarning) : XBrushes.Red;
            var cardFont = new XFont(FontFamily, 28, XFontStyle.Bold);
            var labelFont = new XFont(FontFamily, 9, XFontStyle.Regular);

            var rect = new XRect(x, y, 78, 35);
            gfx.DrawRectangle(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1), rect);

            gfx.DrawString(label, labelFont, XBrushes.Gray,
                new XRect(x + 4, y + 2, 70, 12), XStringFormats.TopCenter);
            gfx.DrawString(score.ToString("F0"), cardFont, brush,
                new XRect(x + 4, y + 12, 70, 22), XStringFormats.TopCenter);
        }

        private static void DrawSeverityBar(XGraphics gfx, ref float y, float left, float right,
            string label, int count, XColor color, XFont font)
        {
            int maxBarWidth = 120;
            int barW = count > 0 ? Math.Max(20, Math.Min(maxBarWidth, count * 15)) : 0;

            gfx.DrawString(label, font, XBrushes.Black,
                new XRect(left, y, 100, 16), XStringFormats.TopLeft);

            if (barW > 0)
            {
                var brush = new XSolidBrush(color);
                gfx.DrawRectangle(brush, new XRect(left + 110, y + 2, barW, 12));
            }

            gfx.DrawString(count.ToString(), font, XBrushes.Black,
                new XRect(left + 115 + maxBarWidth, y, 40, 16), XStringFormats.TopLeft);
            y += 18;
        }

        private static float DrawFeatureTable(XGraphics gfx, float y, float left, float right,
            string title, List<FeatureCategoryResult> features,
            XFont font, XFont fontBold, XFont fontSmall)
        {
            gfx.DrawString(title, fontBold, XBrushes.Black,
                new XRect(left, y, right - left, 20), XStringFormats.TopLeft);
            y += 22;

            // 表头
            float[] colX = { left, left + 90, left + 145, left + 175, left + 200, left + 225, left + 250 };
            string[] headers = { "特征名称", "检查项", "致命", "错误", "警告", "提示", "得分" };

            gfx.DrawRectangle(new XSolidBrush(ColorLightBg), new XRect(left, y, right - left, 16));
            for (int i = 0; i < headers.Length; i++)
                gfx.DrawString(headers[i], fontSmall, XBrushes.Black,
                    new XRect(colX[i], y, colX[Math.Min(i + 1, colX.Length - 1)] - colX[i], 16),
                    XStringFormats.CenterLeft);
            y += 16;

            foreach (var f in features)
            {
                gfx.DrawString(f.CategoryNameCn, fontSmall, XBrushes.Black,
                    new XRect(colX[0], y, colX[1] - colX[0], 14), XStringFormats.CenterLeft);
                gfx.DrawString(f.CheckItemCount.ToString(), fontSmall, XBrushes.Black,
                    new XRect(colX[1], y, colX[2] - colX[1], 14), XStringFormats.Center);
                gfx.DrawString(f.CriticalCount.ToString(), fontSmall,
                    f.CriticalCount > 0 ? new XSolidBrush(ColorCritical) : XBrushes.Black,
                    new XRect(colX[2], y, colX[3] - colX[2], 14), XStringFormats.Center);
                gfx.DrawString(f.ErrorCount.ToString(), fontSmall,
                    f.ErrorCount > 0 ? new XSolidBrush(ColorError) : XBrushes.Black,
                    new XRect(colX[3], y, colX[4] - colX[3], 14), XStringFormats.Center);
                gfx.DrawString(f.WarningCount.ToString(), fontSmall,
                    f.WarningCount > 0 ? new XSolidBrush(ColorWarning) : XBrushes.Black,
                    new XRect(colX[4], y, colX[5] - colX[4], 14), XStringFormats.Center);
                gfx.DrawString(f.InfoCount.ToString(), fontSmall,
                    f.InfoCount > 0 ? new XSolidBrush(ColorInfo) : XBrushes.Black,
                    new XRect(colX[5], y, colX[6] - colX[5], 14), XStringFormats.Center);
                gfx.DrawString(f.Score.ToString("F0"), fontSmall,
                    f.Score >= 80 ? XBrushes.Green :
                    f.Score >= 60 ? new XSolidBrush(ColorWarning) : XBrushes.Red,
                    new XRect(colX[6], y, 30, 14), XStringFormats.Center);
                y += 14;
            }

            y += 10;
            gfx.DrawLine(new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 1), left, y, right, y);
            y += 15;
            return y;
        }

        private static float DrawErrorList(XGraphics gfx, float y, float left, float right,
            string title, List<ValidationErrorEntity> errors,
            XFont font, XFont fontBold, XFont fontSmall)
        {
            // 如果空间不够，换页（简化处理：限制显示条数）
            int maxRows = Math.Min(errors.Count, 20);

            gfx.DrawString(title, fontBold, XBrushes.Black,
                new XRect(left, y, right - left, 20), XStringFormats.TopLeft);
            y += 22;

            float[] colX = { left, left + 40, left + 70, left + 140, left + 200 };
            string[] headers = { "等级", "分类", "特征", "错误码", "中文描述" };

            gfx.DrawRectangle(new XSolidBrush(ColorLightBg), new XRect(left, y, right - left, 16));
            for (int i = 0; i < headers.Length; i++)
                gfx.DrawString(headers[i], fontSmall, XBrushes.Black,
                    new XRect(colX[i], y, colX[Math.Min(i + 1, colX.Length - 1)] - colX[i], 16),
                    XStringFormats.CenterLeft);
            y += 16;

            foreach (var e in errors.Take(maxRows))
            {
                var sevColor = e.Severity switch
                {
                    ErrorSeverity.Critical => ColorCritical,
                    ErrorSeverity.Error => ColorError,
                    ErrorSeverity.Warning => ColorWarning,
                    _ => ColorInfo
                };

                gfx.DrawString(e.Severity.ToDisplayText(), fontSmall, new XSolidBrush(sevColor),
                    new XRect(colX[0], y, colX[1] - colX[0], 14), XStringFormats.CenterLeft);
                gfx.DrawString(e.ErrorCategory.ToDisplayText(), fontSmall, XBrushes.Black,
                    new XRect(colX[1], y, colX[2] - colX[1], 14), XStringFormats.CenterLeft);
                gfx.DrawString(e.FeatureCategory ?? "-", fontSmall, XBrushes.Black,
                    new XRect(colX[2], y, colX[3] - colX[2], 14), XStringFormats.CenterLeft);
                gfx.DrawString(e.ErrorCode ?? "-", fontSmall, XBrushes.Gray,
                    new XRect(colX[3], y, colX[4] - colX[3], 14), XStringFormats.CenterLeft);
                gfx.DrawString(Truncate(e.ErrorMessage, 30), fontSmall, XBrushes.Black,
                    new XRect(colX[4], y, right - colX[4], 14), XStringFormats.CenterLeft);
                y += 14;
            }

            if (errors.Count > maxRows)
            {
                gfx.DrawString($"... 共 {errors.Count} 条异常，仅展示前 {maxRows} 条",
                    fontSmall, XBrushes.Gray, new XRect(left, y, right - left, 14), XStringFormats.CenterLeft);
                y += 14;
            }

            return y;
        }

        // ==================== Excel 内部方法 ====================

        private void BuildSummarySheet(IXLWorksheet ws, DataCheckResultDto result)
        {
            ws.Cell("A1").Value = "数据预检报告";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Range("A1:F1").Merge();

            int row = 3;
            AddExcelRow(ws, ref row, "墙体ID", result.WallKey);
            AddExcelRow(ws, ref row, "数据版本", result.Version);
            AddExcelRow(ws, ref row, "GroupId", result.GroupId);
            AddExcelRow(ws, ref row, "操作员", result.Operator);
            AddExcelRow(ws, ref row, "预检耗时", $"{result.DurationMs}ms");
            AddExcelRow(ws, ref row, "BimData 总分", result.BimTotalScore.ToString("F0"));
            AddExcelRow(ws, ref row, "MomData 总分", result.MomTotalScore.ToString("F0"));

            row++;
            ws.Cell(row, 1).Value = "严重等级";
            ws.Cell(row, 2).Value = "数量";
            ws.Range(row, 1, row, 2).Style.Font.Bold = true;
            row++;

            AddExcelRow(ws, ref row, "Critical(致命)", result.CriticalCount.ToString());
            AddExcelRow(ws, ref row, "Error(错误)", result.ErrorCount.ToString());
            AddExcelRow(ws, ref row, "Warning(警告)", result.WarningCount.ToString());
            AddExcelRow(ws, ref row, "Info(提示)", result.InfoCount.ToString());

            row++;
            ws.Cell(row, 1).Value = "预检结果";
            ws.Cell(row, 2).Value = result.IsPassed ? "通过" : "失败";
            var resultCell = ws.Cell(row, 2);
            resultCell.Style.Fill.BackgroundColor = result.IsPassed
                ? XLColor.FromArgb(0x52, 0xC4, 0x1A)
                : XLColor.FromArgb(0xFF, 0x4D, 0x4F);
            resultCell.Style.Font.FontColor = XLColor.White;

            ws.Column(1).Width = 18;
            ws.Column(2).Width = 30;
        }

        private static void BuildFeaturesSheet(IXLWorksheet ws, DataCheckResultDto result)
        {
            ws.Cell("A1").Value = "BimData 特征分类统计";
            ws.Cell("A1").Style.Font.Bold = true;
            int row = 3;

            string[] headers = { "特征名称", "检查项数", "致命", "错误", "警告", "提示", "得分" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            row++;

            foreach (var f in result.BimFeatureResults)
            {
                ws.Cell(row, 1).Value = f.CategoryNameCn;
                ws.Cell(row, 2).Value = f.CheckItemCount;
                ws.Cell(row, 3).Value = f.CriticalCount;
                ws.Cell(row, 4).Value = f.ErrorCount;
                ws.Cell(row, 5).Value = f.WarningCount;
                ws.Cell(row, 6).Value = f.InfoCount;
                ws.Cell(row, 7).Value = f.Score;
                row++;
            }

            ws.Columns(1, 7).AdjustToContents();
        }

        private static void BuildErrorsSheet(IXLWorksheet ws, List<ValidationErrorEntity> errors)
        {
            ws.Cell("A1").Value = "异常清单";
            ws.Cell("A1").Style.Font.Bold = true;
            int row = 3;

            string[] headers = { "严重等级", "分类", "特征类别", "错误码", "中文描述", "英文描述" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            row++;

            foreach (var e in errors)
            {
                ws.Cell(row, 1).Value = e.Severity.ToDisplayText();
                ws.Cell(row, 2).Value = e.ErrorCategory.ToDisplayText();
                ws.Cell(row, 3).Value = e.FeatureCategory ?? "-";
                ws.Cell(row, 4).Value = e.ErrorCode ?? "-";
                ws.Cell(row, 5).Value = e.ErrorMessage;
                ws.Cell(row, 6).Value = e.ErrorMessageEn ?? "-";
                row++;
            }

            ws.Columns(1, 6).AdjustToContents();
            ws.Column(5).Width = 60;
            ws.Column(6).Width = 60;
        }

        private static void BuildBatchSummarySheet(IXLWorksheet ws, BatchCheckSummaryDto summary)
        {
            ws.Cell("A1").Value = "批量预检汇总报告";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Range("A1:G1").Merge();

            int row = 3;
            AddExcelRow(ws, ref row, "预检总数", summary.TotalCount.ToString());
            AddExcelRow(ws, ref row, "已完成", summary.CompletedCount.ToString());
            AddExcelRow(ws, ref row, "通过", summary.PassedCount.ToString());
            AddExcelRow(ws, ref row, "失败", summary.FailedCount.ToString());
            AddExcelRow(ws, ref row, "异常总数", summary.TotalErrors.ToString());
            AddExcelRow(ws, ref row, "总耗时", $"{summary.DurationMs}ms");
            AddExcelRow(ws, ref row, "操作员", summary.Operator);

            row += 2;
            string[] headers = { "墙体ID", "BimScore", "MomScore", "Critical", "Error", "Warn", "Info", "结果" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            row++;

            foreach (var r in summary.WallResults)
            {
                ws.Cell(row, 1).Value = r.WallKey;
                ws.Cell(row, 2).Value = r.BimTotalScore;
                ws.Cell(row, 3).Value = r.MomTotalScore;
                ws.Cell(row, 4).Value = r.CriticalCount;
                ws.Cell(row, 5).Value = r.ErrorCount;
                ws.Cell(row, 6).Value = r.WarningCount;
                ws.Cell(row, 7).Value = r.InfoCount;
                ws.Cell(row, 8).Value = r.IsPassed ? "通过" : "失败";
                row++;
            }

            ws.Columns(1, 8).AdjustToContents();
        }

        // ==================== 辅助 ====================

        private static void AddExcelRow(IXLWorksheet ws, ref int row, string label, string value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        private static string Truncate(string text, int maxLen)
        {
            return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
        }
    }
}
