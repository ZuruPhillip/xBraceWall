using CncWallStation.Services.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;

namespace CncWallStation.ViewModels
{
    public partial class ResolveExceptionViewModel : ObservableObject
    {
        private readonly ILogger<ResolveExceptionViewModel> _logger;
        private readonly IExceptionReportAppService _exceptionReportService;

        private long _reportId;
        private string _title = "解决异常";

        // ═══════════════ 维修信息 ═══════════════
        [ObservableProperty] private string _repairMethod = string.Empty;
        [ObservableProperty] private string _resolver = string.Empty;
        [ObservableProperty] private string _repairDurationText = string.Empty;
        [ObservableProperty] private DateTime _completionTime = DateTime.Now;
        [ObservableProperty] private string _improvementSuggestion = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;

        // 标题
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ResolveExceptionViewModel(
            ILogger<ResolveExceptionViewModel> logger,
            IExceptionReportAppService exceptionReportService)
        {
            _logger = logger;
            _exceptionReportService = exceptionReportService;
        }

        /// <summary>初始化，接收选中的异常项</summary>
        public void Initialize(ExceptionItemViewModel item)
        {
            _reportId = item.Id;
            Title = $"解决异常 - {item.WallIdStr} / {item.ExceptionTypeDisplay}";
        }

        // ═══════════════ 提交命令 ═══════════════
        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (string.IsNullOrWhiteSpace(RepairMethod))
            {
                MessageBox.Show("请填写维修方法", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Resolver))
            {
                MessageBox.Show("请填写解决人员", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 解析维修耗时（数字，单位h）
            decimal? repairDuration = null;
            if (!string.IsNullOrWhiteSpace(RepairDurationText))
            {
                if (decimal.TryParse(RepairDurationText.Trim(), out var duration))
                {
                    repairDuration = duration;
                }
                else
                {
                    MessageBox.Show("维修耗时请输入数字（单位h）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                await _exceptionReportService.ResolveReportAsync(
                    _reportId,
                    RepairMethod.Trim(),
                    Resolver.Trim(),
                    repairDuration,
                    CompletionTime,
                    string.IsNullOrWhiteSpace(ImprovementSuggestion) ? null : ImprovementSuggestion.Trim(),
                    string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim());

                _logger.LogInformation("异常报告已解决: Id={Id}", _reportId);

                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解决异常失败: Id={Id}", _reportId);
                MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════ 取消命令 ═══════════════
        [RelayCommand]
        private void Cancel()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool result)
        {
            var window = Application.Current.Windows
                .OfType<Views.ResolveExceptionWindow>()
                .FirstOrDefault();
            if (window != null)
            {
                window.DialogResult = result;
                window.Close();
            }
        }
    }
}
