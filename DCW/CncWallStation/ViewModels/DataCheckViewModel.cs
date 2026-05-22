using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Services.DataCheck;
using CncWallStation.VersionMappers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace CncWallStation.ViewModels
{
    public partial class DataCheckViewModel : ObservableObject
    {
        private readonly ILogger<DataCheckViewModel> _logger;
        private readonly IDataCheckService _dataCheckService;
        private readonly IWallAppService _wallAppService;
        private readonly ExportService _exportService;

        // ==================== 搜索区域 ====================

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private string _searchWallId = string.Empty;

        [ObservableProperty]
        private string _selectedVersion = "latest";

        [ObservableProperty]
        private ObservableCollection<string> _versionOptions = new() { "latest" };

        // ==================== 墙体信息 ====================

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCheckCommand))]
        private bool _hasWallInfo;

        // 墙体详情行显示字段（与 BimDataRenderPage 保持一致）
        [ObservableProperty]
        private string _detailWallId = "—";

        [ObservableProperty]
        private string _detailProject = "—";

        [ObservableProperty]
        private string _detailFloor = "—";

        [ObservableProperty]
        private string _detailStage = "—";

        [ObservableProperty]
        private string _detailVersion = "—";

        [ObservableProperty]
        private string _detailImportTime = "—";

        [ObservableProperty]
        private long _currentWallId;  // DB 主键

        [ObservableProperty]
        private string _currentOperator = string.Empty;

        // ==================== 预检状态 ====================

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartCheckCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
        private bool _isChecking;

        [ObservableProperty]
        private string _checkStatusText = "就绪";

        [ObservableProperty]
        private double _checkProgress;

        [ObservableProperty]
        private bool _hasCheckResult;

        // ==================== 预检结果 ====================

        [ObservableProperty]
        private DataCheckResultDto? _checkResult;

        [ObservableProperty]
        private double _bimTotalScore;

        [ObservableProperty]
        private double _momTotalScore;

        [ObservableProperty]
        private int _criticalCount;

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private int _warningCount;

        [ObservableProperty]
        private int _infoCount;

        [ObservableProperty]
        private string _groupInfoText = string.Empty;

        [ObservableProperty]
        private string _checkDurationText = string.Empty;

        [ObservableProperty]
        private bool _isPassed;

        // ==================== 特征分类统计 ====================

        [ObservableProperty]
        private ObservableCollection<FeatureCategoryResult> _bimFeatureResults = new();

        [ObservableProperty]
        private ObservableCollection<FeatureCategoryResult> _momFeatureResults = new();

        // ==================== 异常清单 ====================

        [ObservableProperty]
        private ObservableCollection<ValidationErrorEntity> _allErrors = new();

        [ObservableProperty]
        private ObservableCollection<ValidationErrorEntity> _filteredErrors = new();

        [ObservableProperty]
        private ErrorSeverity? _selectedSeverityFilter;

        // ==================== 批量预检 ====================

        [ObservableProperty]
        private bool _isBatchMode = true;

        [ObservableProperty]
        private string _batchFilterProjectNumber = string.Empty;

        [ObservableProperty]
        private int? _batchFilterFloor;

        [ObservableProperty]
        private bool _batchFilterLatestOnly = true;

        [ObservableProperty]
        private ObservableCollection<string> _batchFilterVersionOptions = new() { "最新版本", "全部版本" };

        [ObservableProperty]
        private string _batchFilterSelectedVersion = "最新版本";

        [ObservableProperty]
        private DateTime? _batchFilterStartTime;

        [ObservableProperty]
        private DateTime? _batchFilterEndTime;

        [ObservableProperty]
        private ObservableCollection<PipelineStage> _batchFilterStages = new();

        [ObservableProperty]
        private int _batchTotalCount;

        [ObservableProperty]
        private int _batchCompletedCount;

        [ObservableProperty]
        private int _batchErrorCount;

        [ObservableProperty]
        private double _batchProgress;

        [ObservableProperty]
        private string _batchStatusText = string.Empty;

        [ObservableProperty]
        private BatchCheckSummaryDto? _batchSummary;

        [ObservableProperty]
        private ObservableCollection<DataCheckResultDto> _topProblemWalls = new();

        // ==================== 历史记录 ====================

        [ObservableProperty]
        private ObservableCollection<DataCheckRecordDto> _historyRecords = new();

        [ObservableProperty]
        private DataCheckRecordDto? _selectedHistory1;

        [ObservableProperty]
        private DataCheckRecordDto? _selectedHistory2;

        [ObservableProperty]
        private HistoryDiffResultDto? _diffResult;

        [ObservableProperty]
        private bool _hasDiffResult;

        // ==================== 构造函数 ====================

        public DataCheckViewModel(
            ILogger<DataCheckViewModel> logger,
            IDataCheckService dataCheckService,
            IWallAppService wallAppService,
            ExportService exportService)
        {
            _logger = logger;
            _dataCheckService = dataCheckService;
            _wallAppService = wallAppService;
            _exportService = exportService;

            // 获取当前 Windows 用户作为操作员
            CurrentOperator = Environment.UserName;
        }

        // ==================== 搜索 ====================

        private bool CanSearch => !string.IsNullOrWhiteSpace(SearchWallId) && !IsChecking;

        [RelayCommand(CanExecute = nameof(CanSearch))]
        private async Task SearchAsync()
        {
            try
            {
                CheckStatusText = "搜索中...";
                HasWallInfo = false;

                var detail = await _wallAppService.GetDetailByWallIdAsync(SearchWallId);

                if (detail == null)
                {
                    CheckStatusText = $"未找到墙体：{SearchWallId}";
                    MessageBox.Show($"未找到墙体：{SearchWallId}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CurrentWallId = detail.Id;

                // 解析 BimJson 中的 schema 版本号
                var resolvedVersion = BimDataVersionResolver.ResolveVersion(detail.BimJsonData);

                // 更新版本下拉框：{ "最新版本(" + version + ")", version }
                VersionOptions = new ObservableCollection<string>
                {
                    $"最新版本({resolvedVersion})",
                    resolvedVersion
                };
                SelectedVersion = VersionOptions[0]; // 默认选"最新版本"

                // 设置墙体详情行显示字段
                DetailWallId = detail.WallId;
                DetailProject = detail.ProjectName;
                DetailFloor = $"楼层 {detail.Floor}";
                DetailStage = detail.PipelineStage.ToDisplayText();
                DetailVersion = $"v{resolvedVersion}";
                DetailImportTime = detail.ImportTime.ToString("yyyy-MM-dd HH:mm");

                HasWallInfo = true;
                CheckStatusText = "已找到墙体，可以执行预检";

                _logger.LogInformation("搜索墙体成功: {WallId}", SearchWallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索墙体失败");
                CheckStatusText = $"搜索失败：{ex.Message}";
                MessageBox.Show($"搜索失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== 单墙预检 ====================

        private bool CanStartCheck => HasWallInfo && !IsChecking;

        [RelayCommand(CanExecute = nameof(CanStartCheck))]
        private async Task StartCheckAsync()
        {
            try
            {
                IsChecking = true;
                HasCheckResult = false;
                CheckProgress = 0;
                CheckStatusText = "正在执行数据预检...";

                var result = await Task.Run(() =>
                    _dataCheckService.CheckSingleWallAsync(CurrentWallId, CurrentOperator)
                );

                ApplyCheckResult(result);
                CheckProgress = 100;
                CheckStatusText = $"预检完成 — {(result.IsPassed ? "通过" : "未通过")}";
                HasCheckResult = true;

                _logger.LogInformation("预检完成: GroupId={GroupId}, Passed={Passed}",
                    result.GroupId, result.IsPassed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预检执行失败");
                CheckStatusText = $"预检失败：{ex.Message}";
                MessageBox.Show($"预检执行失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsChecking = false;
            }
        }

        private void ApplyCheckResult(DataCheckResultDto result)
        {
            CheckResult = result;
            BimTotalScore = result.BimTotalScore;
            MomTotalScore = result.MomTotalScore;
            CriticalCount = result.CriticalCount;
            ErrorCount = result.ErrorCount;
            WarningCount = result.WarningCount;
            InfoCount = result.InfoCount;
            IsPassed = result.IsPassed;
            GroupInfoText = $"GroupId: {result.GroupId} | 耗时: {result.DurationMs}ms | 操作员: {result.Operator}";
            CheckDurationText = $"{result.DurationMs}ms";

            BimFeatureResults = new ObservableCollection<FeatureCategoryResult>(result.BimFeatureResults);
            MomFeatureResults = new ObservableCollection<FeatureCategoryResult>(result.MomFeatureResults);
            AllErrors = new ObservableCollection<ValidationErrorEntity>(result.AllErrors);
            ApplySeverityFilter();
        }

        // ==================== 严重等级筛选 ====================

        partial void OnSelectedSeverityFilterChanged(ErrorSeverity? value)
        {
            ApplySeverityFilter();
        }

        [RelayCommand]
        private void SetSeverityFilter(string? severity)
        {
            SelectedSeverityFilter = severity switch
            {
                "Critical" => ErrorSeverity.Critical,
                "Error" => ErrorSeverity.Error,
                "Warning" => ErrorSeverity.Warning,
                "Info" => ErrorSeverity.Info,
                _ => null
            };
        }

        private void ApplySeverityFilter()
        {
            if (SelectedSeverityFilter == null)
                FilteredErrors = new ObservableCollection<ValidationErrorEntity>(AllErrors);
            else
                FilteredErrors = new ObservableCollection<ValidationErrorEntity>(
                    AllErrors.Where(e => e.Severity == SelectedSeverityFilter.Value));
        }

        // ==================== 批量预检 ====================

        /// <summary>版本选择变更时同步 LatestOnly</summary>
        partial void OnBatchFilterSelectedVersionChanged(string value)
        {
            BatchFilterLatestOnly = value == "最新版本";
        }

        [RelayCommand]
        private void ToggleBatchMode()
        {
            IsBatchMode = !IsBatchMode;
        }

        [RelayCommand]
        private async Task StartBatchCheckAsync()
        {
            try
            {
                IsChecking = true;

                var filter = new WallFilterDto
                {
                    //ProjectName = string.IsNullOrWhiteSpace(BatchFilterProjectName) ? null : BatchFilterProjectName,
                    Floor = BatchFilterFloor,
                    StartTime = BatchFilterStartTime,
                    EndTime = BatchFilterEndTime,
                    PipelineStages = BatchFilterStages.Count > 0 ? BatchFilterStages.ToList() : null
                };

                var progress = new Progress<(int Done, int Total, int Errors)>(p =>
                {
                    BatchCompletedCount = p.Done;
                    BatchTotalCount = p.Total;
                    BatchErrorCount = p.Errors;
                    BatchProgress = p.Total > 0 ? (double)p.Done / p.Total * 100 : 0;
                    BatchStatusText = $"已检 {p.Done}/{p.Total}，发现异常 {p.Errors} 个";
                });

                BatchStatusText = "正在批量预检...";
                var summary = await Task.Run(() =>
                    _dataCheckService.CheckBatchAsync(filter, CurrentOperator, progress)
                );

                BatchSummary = summary;
                TopProblemWalls = new ObservableCollection<DataCheckResultDto>(summary.TopProblemWalls);
                BatchStatusText = $"批量预检完成 — 总数:{summary.TotalCount} 通过:{summary.PassedCount} 失败:{summary.FailedCount}";

                _logger.LogInformation("批量预检完成: Total={Total}, Passed={Passed}, Failed={Failed}",
                    summary.TotalCount, summary.PassedCount, summary.FailedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量预检失败");
                BatchStatusText = $"批量预检失败：{ex.Message}";
                MessageBox.Show($"批量预检失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsChecking = false;
            }
        }

        // ==================== 历史记录 ====================

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            try
            {
                var records = await _dataCheckService.GetHistoryAsync(CurrentWallId);
                HistoryRecords = new ObservableCollection<DataCheckRecordDto>(records);
                _logger.LogInformation("加载历史记录: {Count} 条", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载历史记录失败");
                MessageBox.Show($"加载历史记录失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CompareDiffAsync()
        {
            try
            {
                if (SelectedHistory1 == null || SelectedHistory2 == null)
                {
                    MessageBox.Show("请选择两次预检记录进行对比", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (SelectedHistory1.GroupId == SelectedHistory2.GroupId)
                {
                    MessageBox.Show("请选择不同的记录进行对比", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DiffResult = await _dataCheckService.CompareAsync(
                    SelectedHistory1.GroupId, SelectedHistory2.GroupId);
                HasDiffResult = true;

                _logger.LogInformation("差异对比完成: New={New}, Fixed={Fixed}, Persistent={Persistent}",
                    DiffResult.NewErrors.Count, DiffResult.FixedErrors.Count, DiffResult.PersistentErrors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "差异对比失败");
                MessageBox.Show($"差异对比失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== 导出 ====================

        private bool CanExport => HasCheckResult && !IsChecking;

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportPdfAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PDF 文件|*.pdf",
                    FileName = $"数据预检报告_{SearchWallId}_{DateTime.Now:yyyyMMddHHmmss}.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _exportService.ExportPdfAsync(CheckResult!, dialog.FileName);
                    MessageBox.Show($"PDF 报告已导出：{dialog.FileName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF 导出失败");
                MessageBox.Show($"PDF 导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportExcelAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 文件|*.xlsx",
                    FileName = $"数据预检报告_{SearchWallId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _exportService.ExportExcelAsync(CheckResult!, dialog.FileName);
                    MessageBox.Show($"Excel 报告已导出：{dialog.FileName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel 导出失败");
                MessageBox.Show($"Excel 导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportBatchExcelAsync()
        {
            try
            {
                if (BatchSummary == null) return;

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 文件|*.xlsx",
                    FileName = $"批量预检汇总_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _exportService.ExportBatchExcelAsync(BatchSummary, dialog.FileName);
                    MessageBox.Show($"批量预检 Excel 已导出：{dialog.FileName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量预检 Excel 导出失败");
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
