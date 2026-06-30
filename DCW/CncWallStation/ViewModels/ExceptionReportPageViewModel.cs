using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        // 操作人（可从控制页传入，默认"操作员"）
        [ObservableProperty] private string _operatorName = "操作员";

        public ExceptionReportPageViewModel(
            ILogger<ExceptionReportPageViewModel> logger,
            IExceptionReportAppService exceptionReportService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _exceptionReportService = exceptionReportService;
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

                var result = await _exceptionReportService.GetPagedReportsAsync(
                    filterWallId, PageIndex, DefaultPageSize);

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

        // ═══════════════ 标记已解决 ═══════════════
        [RelayCommand]
        private async Task ResolveReportAsync(ExceptionItemViewModel? item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                $"确认将该异常标记为已解决？\n墙体: {item.WallIdStr}\n类型: {item.ExceptionTypeDisplay}",
                "确认已解决", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _exceptionReportService.ResolveReportAsync(item.Id);
                item.IsResolved = true;
                _logger.LogInformation("异常报告已标记解决: Id={Id}", item.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记已解决失败");
                MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string Operator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }

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
                Operator = dto.Operator,
                CreatedAt = dto.CreatedAt,
                IsResolved = dto.IsResolved
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
}
