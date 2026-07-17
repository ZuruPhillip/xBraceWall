using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.IO;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 异常报告 PDF 导出服务
    /// </summary>
    public class ExceptionReportExportService
    {
        private readonly ILogger<ExceptionReportExportService> _logger;

        private const string FontFamily = "Microsoft YaHei";

        private static readonly XColor ColorDark = XColor.FromArgb(255, 0x26, 0x26, 0x26);
        private static readonly XColor ColorGray = XColor.FromArgb(255, 0x8C, 0x8C, 0x8C);
        private static readonly XColor ColorLightBg = XColor.FromArgb(255, 0xF5, 0xF5, 0xF5);
        private static readonly XColor ColorBorder = XColor.FromArgb(255, 0xE0, 0xE0, 0xE0);
        private static readonly XColor ColorHeaderBg = XColor.FromArgb(255, 0xE6, 0xF7, 0xFF);
        private static readonly XColor ColorPrimary = XColor.FromArgb(255, 0x18, 0x90, 0xFF);

        public ExceptionReportExportService(ILogger<ExceptionReportExportService> logger)
        {
            _logger = logger;
        }

        public async Task ExportAsync(List<ExceptionReportDto> reports, string filePath)
        {
            using var doc = new PdfDocument();
            doc.Info.Title = "异常报告导出";

            var font = new XFont(FontFamily, 9, XFontStyle.Regular);
            var fontBold = new XFont(FontFamily, 10, XFontStyle.Bold);
            var fontHeader = new XFont(FontFamily, 16, XFontStyle.Bold);
            var fontSmall = new XFont(FontFamily, 8, XFontStyle.Regular);

            // A4 纵向
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(210);
            page.Height = XUnit.FromMillimeter(297);
            var gfx = XGraphics.FromPdfPage(page);

            float y = 25;
            float leftMargin = 25;
            float rightMargin = (float)page.Width.Point - 25;
            float pageWidth = (float)page.Width.Point;
            float pageHeight = (float)page.Height.Point;
            float contentWidth = rightMargin - leftMargin;

            // 标题
            gfx.DrawString("异常报告导出", fontHeader, XBrushes.DarkRed,
                new XRect(leftMargin, y, contentWidth, 25), XStringFormats.TopLeft);
            y += 26;

            gfx.DrawString($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}    记录数：{reports.Count} 条",
                fontSmall, XBrushes.Gray,
                new XRect(leftMargin, y, contentWidth, 14), XStringFormats.TopLeft);
            y += 16;

            gfx.DrawLine(new XPen(ColorBorder, 1), leftMargin, y, rightMargin, y);
            y += 8;

            for (int i = 0; i < reports.Count; i++)
            {
                var r = reports[i];

                // 检查是否需要分页（每条记录预估高度）
                float estimatedHeight = 180;
                var photos = GetPhotoList(r.PhotoPaths);
                if (photos.Count > 0)
                {
                    estimatedHeight += 110 * ((photos.Count + 1) / 2);
                }

                if (y + estimatedHeight > pageHeight - 30)
                {
                    // 新页
                    page = doc.AddPage();
                    page.Width = XUnit.FromMillimeter(210);
                    page.Height = XUnit.FromMillimeter(297);
                    gfx = XGraphics.FromPdfPage(page);
                    y = 25;
                }

                y = DrawReportBlock(gfx, r, i + 1, y, leftMargin, rightMargin, font, fontBold, fontSmall,
                    pageHeight, doc, ref page, ref gfx);
                y += 8;
            }

            // 页脚
            gfx.DrawString($"报告生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                fontSmall, XBrushes.Gray,
                new XRect(leftMargin, pageHeight - 20, contentWidth, 10), XStringFormats.TopLeft);

            doc.Save(filePath);
            _logger.LogInformation("异常报告 PDF 已导出：{Path}, 记录数={Count}", filePath, reports.Count);

            await Task.CompletedTask;
        }

        private float DrawReportBlock(XGraphics gfx, ExceptionReportDto r, int index, float y,
            float left, float right, XFont font, XFont fontBold, XFont fontSmall,
            float pageHeight, PdfDocument doc, ref PdfPage page, ref XGraphics gfxRef)
        {
            float contentWidth = right - left;
            float pageWidth = (float)page.Width.Point;

            // 记录标题
            var titleBg = new XSolidBrush(ColorHeaderBg);
            gfx.DrawRectangle(titleBg, new XRect(left, y, contentWidth, 20));
            gfx.DrawString($"#{index}  墙体：{r.WallIdStr}    类型：{GetExceptionTypeDisplay(r)}",
                fontBold, new XSolidBrush(ColorPrimary),
                new XRect(left + 6, y, contentWidth - 12, 20), XStringFormats.CenterLeft);
            y += 22;

            // 信息字段（两列布局）
            var fields = new (string label, string value)[]
            {
                ("登记人", r.Registrant ?? "-"),
                ("故障频次", r.FrequencyCount.ToString()),
                ("登记时间", r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                ("发生时间", r.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss")),
                ("状态", r.IsResolved ? "已解决" : "未解决"),
                ("故障描述", r.Description ?? "-"),
                ("维修方法", r.RepairMethod ?? "-"),
                ("解决人员", r.Resolver ?? "-"),
                ("维修耗时", r.RepairDuration.HasValue ? $"{r.RepairDuration.Value} h" : "-"),
                ("完成时间", r.CompletionTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"),
                ("机构改善建议", r.ImprovementSuggestion ?? "-"),
                ("备注", r.Remarks ?? "-")
            };

            float labelWidth = 80;
            float colWidth = contentWidth / 2;
            float rowHeight = 16;

            for (int i = 0; i < fields.Length; i++)
            {
                // 分页检查
                if (y + rowHeight > pageHeight - 30)
                {
                    page = doc.AddPage();
                    page.Width = XUnit.FromMillimeter(210);
                    page.Height = XUnit.FromMillimeter(297);
                    gfxRef = XGraphics.FromPdfPage(page);
                    gfx = gfxRef;
                    y = 25;
                }

                int col = i % 2;
                float x = left + col * colWidth;

                // 故障描述、维修方法、机构改善建议、备注 跨两列
                bool isFullRow = i >= 5;

                if (isFullRow)
                {
                    // 前面已排完两列的短字段，后面每个字段占满一行
                    if (col == 1) continue; // 跳过偶数位置（已在前面处理）
                }

                if (isFullRow)
                {
                    float actualIndex = 5 + (i - 5);
                    if (actualIndex >= fields.Length) break;
                    var f = fields[(int)actualIndex];
                    gfx.DrawString(f.label, font, new XSolidBrush(ColorGray),
                        new XRect(x, y, labelWidth, rowHeight), XStringFormats.CenterLeft);
                    // 长文本自动换行
                    var valueRect = new XRect(x + labelWidth, y, contentWidth - labelWidth, rowHeight);
                    DrawWrappedText(gfx, f.value, font, new XSolidBrush(ColorDark), valueRect, ref y, rowHeight,
                        pageHeight, doc, ref page, ref gfxRef, left, right);
                    y += 4;
                }
                else
                {
                    gfx.DrawString(fields[i].label, font, new XSolidBrush(ColorGray),
                        new XRect(x, y, labelWidth, rowHeight), XStringFormats.CenterLeft);
                    gfx.DrawString(fields[i].value, font, new XSolidBrush(ColorDark),
                        new XRect(x + labelWidth, y, colWidth - labelWidth - 6, rowHeight),
                        XStringFormats.CenterLeft);
                }

                if (!isFullRow && col == 1)
                {
                    y += rowHeight + 2;
                }
            }

            y += 6;

            // 现场照片
            var photos = GetPhotoList(r.PhotoPaths);
            if (photos.Count > 0)
            {
                gfx.DrawString("现场照片", fontBold, new XSolidBrush(ColorGray),
                    new XRect(left, y, contentWidth, 16), XStringFormats.CenterLeft);
                y += 20;

                float imgWidth = (contentWidth - 10) / 2;
                float imgHeight = 100;
                int colIndex = 0;

                foreach (var photoPath in photos)
                {
                    if (!File.Exists(photoPath)) continue;

                    // 分页检查
                    if (y + imgHeight + 6 > pageHeight - 30)
                    {
                        page = doc.AddPage();
                        page.Width = XUnit.FromMillimeter(210);
                        page.Height = XUnit.FromMillimeter(297);
                        gfxRef = XGraphics.FromPdfPage(page);
                        gfx = gfxRef;
                        y = 25;
                    }

                    float imgX = left + colIndex * (imgWidth + 10);

                    try
                    {
                        using var img = XImage.FromFile(photoPath);
                        // 等比缩放
                        double ratio = Math.Min(imgWidth / img.PixelWidth, imgHeight / img.PixelHeight);
                        double drawW = img.PixelWidth * ratio;
                        double drawH = img.PixelHeight * ratio;
                        float drawX = imgX + (float)((imgWidth - drawW) / 2);
                        float drawY = y;

                        gfx.DrawImage(img, drawX, drawY, drawW, drawH);

                        // 边框
                        gfx.DrawRectangle(new XPen(ColorBorder, 0.5),
                            new XRect(imgX, y, imgWidth, imgHeight));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PDF 图片加载失败: {Path}", photoPath);
                        gfx.DrawRectangle(new XSolidBrush(ColorLightBg),
                            new XRect(imgX, y, imgWidth, imgHeight));
                        gfx.DrawString("[图片加载失败]", fontSmall, XBrushes.Gray,
                            new XRect(imgX, y + imgHeight / 2 - 6, imgWidth, 12),
                            XStringFormats.Center);
                    }

                    colIndex++;
                    if (colIndex >= 2)
                    {
                        colIndex = 0;
                        y += imgHeight + 6;
                    }
                }

                if (colIndex > 0)
                {
                    y += imgHeight + 6;
                }
            }

            // 分隔线
            if (y + 4 < pageHeight - 25)
            {
                gfx.DrawLine(new XPen(ColorBorder, 0.5), left, y, right, y);
                y += 6;
            }

            return y;
        }

        private void DrawWrappedText(XGraphics gfx, string text, XFont font, XBrush brush,
            XRect rect, ref float y, float rowHeight, float pageHeight,
            PdfDocument doc, ref PdfPage page, ref XGraphics gfxRef,
            float left, float right)
        {
            if (string.IsNullOrEmpty(text)) return;

            float maxWidth = (float)rect.Width;
            var words = text.ToCharArray();
            var line = string.Empty;
            float lineY = y;

            foreach (var ch in words)
            {
                var testLine = line + ch;
                var size = gfx.MeasureString(testLine, font);
                if (size.Width > maxWidth && line.Length > 0)
                {
                    gfx.DrawString(line, font, brush,
                        new XRect(rect.X, lineY, maxWidth, rowHeight), XStringFormats.TopLeft);
                    lineY += rowHeight;
                    line = ch.ToString();

                    // 分页检查
                    if (lineY + rowHeight > pageHeight - 30)
                    {
                        page = doc.AddPage();
                        page.Width = XUnit.FromMillimeter(210);
                        page.Height = XUnit.FromMillimeter(297);
                        gfxRef = XGraphics.FromPdfPage(page);
                        gfx = gfxRef;
                        lineY = 25;
                    }
                }
                else
                {
                    line = testLine;
                }
            }

            if (!string.IsNullOrEmpty(line))
            {
                gfx.DrawString(line, font, brush,
                    new XRect(rect.X, lineY, maxWidth, rowHeight), XStringFormats.TopLeft);
                y = lineY;
            }
        }

        private static string GetExceptionTypeDisplay(ExceptionReportDto r)
        {
            if (!string.IsNullOrWhiteSpace(r.CustomType))
                return r.CustomType;
            return ((Models.Enums.ExceptionType)r.ExceptionType).ToDisplayText();
        }

        private static List<string> GetPhotoList(string? photoPathsJson)
        {
            if (string.IsNullOrWhiteSpace(photoPathsJson))
                return new List<string>();

            try
            {
                var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(photoPathsJson);
                return paths ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
