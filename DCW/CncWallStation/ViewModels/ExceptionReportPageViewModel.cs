using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CncWallStation.ViewModels
{
    public partial class ExceptionReportPageViewModel : ObservableObject
    {
        private readonly ILogger<ExceptionReportPageViewModel> _logger;
        private readonly IExceptionReportAppService _exceptionReportService;
        private readonly ExceptionReportExportService _exportService;
        private readonly IServiceProvider _serviceProvider;

        private long _currentWallId;
        private bool _isInitialized;
        private const int DefaultPageSize = 20;

        // ═══════════════ 分页 ═══════════════
        [ObservableProperty] private int _pageIndex;
        [ObservableProperty] private int _totalPages;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private bool _canPreviousPage;
        [ObservableProperty] private bool _canNextPage;

        /// <summary>显示用的页码（从 1 开始）</summary>
        public int DisplayPageIndex => PageIndex + 1;
        partial void OnPageIndexChanged(int value) => OnPropertyChanged(nameof(DisplayPageIndex));

        // ═══════════════ 历史异常列表 ═══════════════
        public ObservableCollection<ExceptionItemViewModel> ExceptionReports { get; } = new();

        [ObservableProperty] private ExceptionItemViewModel? _selectedReport;
        [ObservableProperty] private string _pageSubtitle = "历史异常记录";

        // 登记人（可从控制页传入）
        [ObservableProperty] private string _registrantName = "操作员";

        // ═══════════════ 查询条件 ═══════════════
        /// <summary>异常类型筛选（-1=全部）</summary>
        [ObservableProperty] private int _selectedExceptionTypeFilter = -1;
        /// <summary>开始日期</summary>
        [ObservableProperty] private DateTime? _startDate;
        /// <summary>结束日期</summary>
        [ObservableProperty] private DateTime? _endDate;
        /// <summary>是否解决筛选（-1=全部/0=未解决/1=已解决）</summary>
        [ObservableProperty] private int _selectedResolvedFilter = -1;

        /// <summary>异常类型筛选选项（含"全部"）</summary>
        public ObservableCollection<ExceptionTypeItem> ExceptionTypeFilters { get; } = new()
        {
            new() { Name = "全部", Value = -1 },
            new() { Name = "主轴异常", Value = 0 },
            new() { Name = "PLC通讯异常", Value = 1 },
            new() { Name = "刀具断裂", Value = 2 },
            new() { Name = "材料缺陷", Value = 3 },
            new() { Name = "安全门异常", Value = 4 },
            new() { Name = "进给异常", Value = 5 },
            new() { Name = "其他", Value = 6 }
        };

        /// <summary>是否解决筛选选项</summary>
        public ObservableCollection<ResolvedFilterItem> ResolvedFilters { get; } = new()
        {
            new() { Name = "全部", Value = -1 },
            new() { Name = "未解决", Value = 0 },
            new() { Name = "已解决", Value = 1 }
        };

        public ExceptionReportPageViewModel(
            ILogger<ExceptionReportPageViewModel> logger,
            IExceptionReportAppService exceptionReportService,
            ExceptionReportExportService exportService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _exceptionReportService = exceptionReportService;
            _exportService = exportService;
            _serviceProvider = serviceProvider;
        }

        // ═══════════════ 初始化 ═══════════════
        public async Task InitializeAsync(long? wallId = null, string? wallIdStr = null)
        {
            _currentWallId = wallId ?? 0;
            PageSubtitle = wallId.HasValue && wallId.Value > 0
                ? $"墙体: {wallIdStr ?? wallId.Value.ToString()}"
                : "历史异常记录";

            PageIndex = 0;
            _isInitialized = true;
            await LoadPagedHistoryAsync();
        }

        /// <summary>确保数据已加载（仅首次调用时触发查询）</summary>
        public async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        // ═══════════════ 分页加载 ═══════════════
        private async Task LoadPagedHistoryAsync()
        {
            try
            {
                long? filterWallId = _currentWallId > 0 ? _currentWallId : null;
                int? filterType = SelectedExceptionTypeFilter >= 0 ? SelectedExceptionTypeFilter : null;
                bool? filterResolved = SelectedResolvedFilter switch
                {
                    0 => false,
                    1 => true,
                    _ => null
                };

                var result = await _exceptionReportService.GetPagedReportsAsync(
                    filterWallId, filterType, StartDate, EndDate, filterResolved,
                    PageIndex, DefaultPageSize);

                TotalCount = result.TotalCount;
                TotalPages = TotalCount > 0
                    ? (int)Math.Ceiling((double)TotalCount / DefaultPageSize)
                    : 1;

                CanPreviousPage = PageIndex > 0;
                CanNextPage = PageIndex < TotalPages - 1;

                ExceptionReports.Clear();
                int rowIndex = PageIndex * DefaultPageSize + 1;
                foreach (var r in result.Items)
                {
                    var item = ExceptionItemViewModel.FromDto(r);
                    item.RowIndex = rowIndex++;
                    ExceptionReports.Add(item);
                }

                _logger.LogInformation("分页加载异常历史: Page={Page}, Total={Total}, Count={Count}",
                    PageIndex, TotalCount, result.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分页加载异常历史失败");
            }
        }

        // ═══════════════ 查询 ═══════════════
        [RelayCommand]
        private async Task SearchAsync()
        {
            PageIndex = 0;
            await LoadPagedHistoryAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SelectedExceptionTypeFilter = -1;
            StartDate = null;
            EndDate = null;
            SelectedResolvedFilter = -1;
            PageIndex = 0;
            _ = LoadPagedHistoryAsync();
        }

        // ═══════════════ 分页导航 ═══════════════
        [RelayCommand]
        private async Task PreviousPageAsync()
        {
            if (PageIndex > 0)
            {
                PageIndex--;
                await LoadPagedHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task NextPageAsync()
        {
            if (PageIndex < TotalPages - 1)
            {
                PageIndex++;
                await LoadPagedHistoryAsync();
            }
        }

        // ═══════════════ 解决异常（弹窗） ═══════════════
        [RelayCommand]
        private async Task ResolveReportAsync(ExceptionItemViewModel? item)
        {
            if (item == null) return;

            await Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var vm = ActivatorUtilities.CreateInstance<ResolveExceptionViewModel>(_serviceProvider);
                    vm.Initialize(item);

                    var window = ActivatorUtilities.CreateInstance<ResolveExceptionWindow>(_serviceProvider, vm);
                    window.Owner = Application.Current.MainWindow;
                    var result = window.ShowDialog();

                    // 解决成功后刷新列表
                    if (result == true)
                    {
                        _ = LoadPagedHistoryAsync();
                        _logger.LogInformation("异常报告已解决: Id={Id}", item.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "打开解决异常窗口失败");
                    MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));
        }

        // ═══════════════ 导出 PDF ═══════════════
        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            try
            {
                long? filterWallId = _currentWallId > 0 ? _currentWallId : null;
                int? filterType = SelectedExceptionTypeFilter >= 0 ? SelectedExceptionTypeFilter : null;
                bool? filterResolved = SelectedResolvedFilter switch
                {
                    0 => false,
                    1 => true,
                    _ => null
                };

                var allReports = await _exceptionReportService.GetAllReportsForExportAsync(
                    filterWallId, filterType, StartDate, EndDate, filterResolved);

                if (allReports.Count == 0)
                {
                    MessageBox.Show("当前查询条件下无数据可导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dlg = new SaveFileDialog
                {
                    Title = "导出异常报告 PDF",
                    Filter = "PDF 文件|*.pdf",
                    FileName = $"异常报告_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                if (dlg.ShowDialog() != true) return;

                await _exportService.ExportAsync(allReports, dlg.FileName);

                MessageBox.Show($"导出成功，共 {allReports.Count} 条记录\n{dlg.FileName}",
                    "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);

                _logger.LogInformation("异常报告 PDF 已导出: Count={Count}, Path={Path}", allReports.Count, dlg.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出异常报告 PDF 失败");
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════ 查看异常详情 ═══════════════
        [RelayCommand]
        private void ViewReport(ExceptionItemViewModel? item)
        {
            if (item == null) return;
            OpenDetailWindow(item, isEdit: false);
        }

        // ═══════════════ 编辑异常报告 ═══════════════
        [RelayCommand]
        private void EditReport(ExceptionItemViewModel? item)
        {
            if (item == null) return;
            OpenDetailWindow(item, isEdit: true);
        }

        private void OpenDetailWindow(ExceptionItemViewModel item, bool isEdit)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var vm = ActivatorUtilities.CreateInstance<ExceptionDetailViewModel>(_serviceProvider);
                    if (isEdit)
                        vm.InitializeForEdit(item);
                    else
                        vm.InitializeForView(item);

                    var window = ActivatorUtilities.CreateInstance<ExceptionDetailWindow>(_serviceProvider, vm);
                    window.Owner = Application.Current.MainWindow;
                    var result = window.ShowDialog();

                    // 编辑模式保存后刷新列表
                    if (isEdit && result == true)
                    {
                        _ = LoadPagedHistoryAsync();
                    }

                    _logger.LogInformation("异常详情窗口已关闭: Id={Id}, IsEdit={IsEdit}", item.Id, isEdit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "打开异常详情窗口失败");
                }
            });
        }
    }

    /// <summary>
    /// 异常历史列表项 ViewModel
    /// </summary>
    public class ExceptionItemViewModel
    {
        public int RowIndex { get; set; }
        public long Id { get; set; }
        public long WallId { get; set; }
        /// <summary>墙体字符串标识（来自 Wall 表）</summary>
        public string WallIdStr { get; set; } = string.Empty;
        public int ExceptionType { get; set; }
        public string? CustomType { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PhotoPaths { get; set; }
        public string Registrant { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime OccurredAt { get; set; }
        public int FrequencyCount { get; set; }
        public bool IsResolved { get; set; }
        public string? RepairMethod { get; set; }
        public string? Resolver { get; set; }
        public decimal? RepairDuration { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string? ImprovementSuggestion { get; set; }
        public string? Remarks { get; set; }

        /// <summary>维修耗时显示（带 h 后缀）</summary>
        public string RepairDurationDisplay => RepairDuration.HasValue ? $"{RepairDuration.Value} h" : "-";

        /// <summary>完成时间显示</summary>
        public string CompletionTimeDisplay => CompletionTime?.ToString("yyyy-MM-dd HH:mm") ?? "-";

        /// <summary>维修方法显示</summary>
        public string RepairMethodDisplay => string.IsNullOrWhiteSpace(RepairMethod) ? "-" : RepairMethod;

        /// <summary>解决人员显示</summary>
        public string ResolverDisplay => string.IsNullOrWhiteSpace(Resolver) ? "-" : Resolver;

        /// <summary>机构改善建议显示</summary>
        public string ImprovementSuggestionDisplay => string.IsNullOrWhiteSpace(ImprovementSuggestion) ? "-" : ImprovementSuggestion;

        /// <summary>备注显示</summary>
        public string RemarksDisplay => string.IsNullOrWhiteSpace(Remarks) ? "-" : Remarks;

        public string ExceptionTypeDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomType))
                    return CustomType;
                return ((Models.Enums.ExceptionType)ExceptionType).ToDisplayText();
            }
        }

        public static ExceptionItemViewModel FromDto(ExceptionReportDto dto)
        {
            return new ExceptionItemViewModel
            {
                Id = dto.Id,
                WallId = dto.WallId,
                WallIdStr = dto.WallIdStr,
                ExceptionType = dto.ExceptionType,
                CustomType = dto.CustomType,
                Description = dto.Description,
                PhotoPaths = dto.PhotoPaths,
                Registrant = dto.Registrant,
                CreatedAt = dto.CreatedAt,
                OccurredAt = dto.OccurredAt,
                FrequencyCount = dto.FrequencyCount,
                IsResolved = dto.IsResolved,
                RepairMethod = dto.RepairMethod,
                Resolver = dto.Resolver,
                RepairDuration = dto.RepairDuration,
                CompletionTime = dto.CompletionTime,
                ImprovementSuggestion = dto.ImprovementSuggestion,
                Remarks = dto.Remarks
            };
        }
    }

    /// <summary>
    /// 异常类型下拉项
    /// </summary>
    public class ExceptionTypeItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    /// <summary>
    /// 是否解决筛选项
    /// </summary>
    public class ResolvedFilterItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
