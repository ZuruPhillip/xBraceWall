using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Services.OpcUa;
using CncWallStation.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace CncWallStation.ViewModels
{
    public partial class ControllerPageViewModel : ObservableObject
    {
        private readonly ILogger<ControllerPageViewModel> _logger;
        private readonly IOpcUaService _opcUaService;
        private readonly IMachiningAppService _machiningAppService;
        private readonly IServiceProvider _serviceProvider;

        // 计时器
        private readonly DispatcherTimer _machiningTimer;
        private DateTime _machiningStartTime;
        private bool _isTimerRunning;

        // 当前墙体数据
        [ObservableProperty] private WallInfoDto? _currentWall;
        [ObservableProperty] private WallQueueItemDto? _selectedQueueItem;

        // 队列
        public ObservableCollection<WallQueueItemDto> WallQueue { get; } = new();

        // 实时参数
        [ObservableProperty] private RealtimeParamsDto _realtimeParams = new();

        // PLC 数据
        public ObservableCollection<PlcLineDataDto> PlcLineData { get; } = new();

        // OPC 连接状态
        [ObservableProperty] private bool _isOpcConnected;

        // 分页
        private const int DefaultPageSize = 10;
        [ObservableProperty] private int _pageIndex;
        [ObservableProperty] private int _totalPages;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private bool _canPreviousPage;
        [ObservableProperty] private bool _canNextPage;
        public ObservableCollection<PlcLineDataDto> PagedPlcLineData { get; } = new();

        /// <summary>显示用的页码（从 1 开始）</summary>
        public int DisplayPageIndex => PageIndex + 1;
        partial void OnPageIndexChanged(int value) => OnPropertyChanged(nameof(DisplayPageIndex));

        // 加工状态
        [ObservableProperty] private string _machiningDurationText = "00:00:00";
        [ObservableProperty] private string _operatorName = "操作员";
        [ObservableProperty] private string _statusText = "就绪";
        [ObservableProperty] private string _statusBadgeBackground = "#1677FF";
        [ObservableProperty] private string _statusIndicatorColor = "#FFFFFF";
        [ObservableProperty] private string _progressText = "0%";

        // 按钮启用状态
        [ObservableProperty] private bool _canStart = true;
        [ObservableProperty] private bool _canPause;
        [ObservableProperty] private bool _canEmergencyStop;
        [ObservableProperty] private bool _canReset;
        [ObservableProperty] private bool _canMarkException;
        [ObservableProperty] private bool _canComplete;

        // 参数状态
        [ObservableProperty] private string _spindleStatusColor = "#9E9E9E";
        [ObservableProperty] private string _feedRateStatusColor = "#9E9E9E";
        [ObservableProperty] private string _tableReadyColor = "#9E9E9E";
        [ObservableProperty] private string _safetyDoorColor = "#9E9E9E";
        [ObservableProperty] private string _stageBackground = "#334155";

        public ControllerPageViewModel(
            ILogger<ControllerPageViewModel> logger,
            IOpcUaService opcUaService,
            IMachiningAppService machiningAppService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _opcUaService = opcUaService;
            _machiningAppService = machiningAppService;
            _serviceProvider = serviceProvider;

            _machiningTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _machiningTimer.Tick += OnTimerTick;

            // OPC 订阅回调
            _opcUaService.NodeValuesUpdated += OnOpcNodeValuesUpdated;

            // OPC 状态订阅
            IsOpcConnected = _opcUaService.IsConnected;
            _opcUaService.StatusChanged += OnOpcStatusChanged;
        }

        // ═══════════════ 初始化 ═══════════════
        public async Task InitializeAsync()
        {
            try
            {
                // 直接加载队列（触发 LoadWallAsync → Load3DWallDataAsync 完整管道）
                await LoadQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化 ControllerPage 失败");
            }
        }

        // ═══════════════ 墙体队列 ═══════════════
        [RelayCommand]
        private async Task LoadQueueAsync()
        {
            try
            {
                var queue = await _machiningAppService.GetWallQueueAsync(5);
                WallQueue.Clear();
                foreach (var item in queue)
                    WallQueue.Add(item);

                if (WallQueue.Count > 0)
                    SelectedQueueItem = WallQueue[0];

                _logger.LogInformation("加载加工队列: {Count} 条", queue.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载队列失败");
            }
        }

        partial void OnSelectedQueueItemChanged(WallQueueItemDto? value)
        {
            if (value != null)
                _ = LoadWallAsync(value);
        }

        private async Task LoadWallAsync(WallQueueItemDto item)
        {
            try
            {
                var info = await _machiningAppService.GetWallInfoAsync(item.WallId);
                CurrentWall = info;

                if (info != null)
                {
                    StageBackground = GetStageColor(info.Status);

                    // 初始化 PLC 数据行
                    await InitPlcLineDataAsync(info.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载墙体失败: WallId={WallId}", item.WallId);
            }
        }

        private const int DefaultPlcLineCount = 100;

        private async Task InitPlcLineDataAsync(long wallId)
        {
            PlcLineData.Clear();
            try
            {
                var count = DefaultPlcLineCount;

                for (int i = 0; i < count; i++)
                {
                    PlcLineData.Add(new PlcLineDataDto { Index = i });
                }

                // 订阅 PLC 节点
                await SubscribePlcNodesAsync(count);

                _logger.LogInformation("初始化 PLC 数据: {Count} 行", count);

                // 初始化首屏分页
                RefreshPagedData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化 PLC 数据失败");
            }
        }

        private async Task SubscribePlcNodesAsync(int count)
        {
            if (!_opcUaService.IsConnected) return;

            try
            {
                var nodes = new System.Collections.Generic.List<OpcNodeConfig>();
                string[] headers = { "T", "F", "D", "X0", "Y0", "Z0", "X1", "Y1", "Z1" };

                for (int i = 0; i < count; i++)
                {
                    foreach (var h in headers)
                    {
                        nodes.Add(new OpcNodeConfig
                        {
                            NodeId = $"ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef[{i}].{h}",
                            Description = $"L{i}.{h}"
                        });
                    }
                }

                await _opcUaService.SubscribeNodesAsync(nodes);
                _logger.LogInformation("已订阅 {Count} 个 PLC 节点", nodes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "订阅 PLC 节点失败");
            }
        }

        // ═══════════════ OPC 回调 ═══════════════
        private void OnOpcNodeValuesUpdated(object? sender, System.Collections.Generic.IReadOnlyList<OpcNodeConfig> nodes)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    UpdatePlcLineDataFromOpc(nodes);
                    UpdateRealtimeParamsFromOpc(nodes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OPC 数据更新失败");
                }
            });
        }

        private void OnOpcStatusChanged(object? sender, OpcConnectionStatus status)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsOpcConnected = status == OpcConnectionStatus.Connected;
                if (!IsOpcConnected)
                    RefreshParameterColors();
            });
        }

        private void UpdatePlcLineDataFromOpc(System.Collections.Generic.IReadOnlyList<OpcNodeConfig> nodes)
        {
            var completedIndices = new System.Collections.Generic.List<int>();

            foreach (var node in nodes)
            {
                var match = System.Text.RegularExpressions.Regex.Match(node.NodeId,
                    @"LineDef\[(\d+)\]\.(\w+)");
                if (!match.Success) continue;

                int index = int.Parse(match.Groups[1].Value);
                string header = match.Groups[2].Value;

                if (index < 0 || index >= PlcLineData.Count) continue;

                var line = PlcLineData[index];
                var val = System.Convert.ToSingle(node.CurrentValue ?? 0f);

                switch (header)
                {
                    case "T": line.T = (int)val; break;
                    case "F": line.F = (int)val; break;
                    case "D": line.D = (int)val; break;
                    case "X0": line.X0 = val; break;
                    case "Y0": line.Y0 = val; break;
                    case "Z0": line.Z0 = val; break;
                    case "X1": line.X1 = val; break;
                    case "Y1": line.Y1 = val; break;
                    case "Z1": line.Z1 = val; break;
                }

                // 检查 D 值是否已完成（D=1 表示该行已加工）
                if (header == "D" && (int)val == 1)
                {
                    line.IsCompleted = true;
                    if (!completedIndices.Contains(index))
                        completedIndices.Add(index);
                }
            }

            // 更新加工进度
            if (completedIndices.Count > 0)
            {
                var pct = PlcLineData.Count > 0
                    ? (completedIndices.Count * 100 / PlcLineData.Count) : 0;
                ProgressText = $"{pct}%";
            }
        }

        private void UpdateRealtimeParamsFromOpc(System.Collections.Generic.IReadOnlyList<OpcNodeConfig> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.NodeId.Contains("TableReady"))
                    RealtimeParams.TableReady = System.Convert.ToBoolean(node.CurrentValue ?? false);
                else if (node.NodeId.Contains("SafetyDoor"))
                    RealtimeParams.SafetyDoorClosed = System.Convert.ToBoolean(node.CurrentValue ?? false);
                else if (node.NodeId.Contains("SpindleSpeed"))
                    RealtimeParams.SpindleSpeed = System.Convert.ToDouble(node.CurrentValue ?? 0);
                else if (node.NodeId.Contains("FeedRate"))
                    RealtimeParams.FeedRate = System.Convert.ToDouble(node.CurrentValue ?? 0);
                else if (node.NodeId.Contains("CurrentTool"))
                    RealtimeParams.CurrentTool = System.Convert.ToInt32(node.CurrentValue ?? 0);
            }

            SpindleStatusColor = RealtimeParams.SpindleSpeed > 0 ? "#4CAF50" : "#F44336";
            FeedRateStatusColor = RealtimeParams.FeedRate > 0 ? "#4CAF50" : "#F44336";
            TableReadyColor = RealtimeParams.TableReady ? "#52C41A" : "#E60012";
            SafetyDoorColor = RealtimeParams.SafetyDoorClosed ? "#52C41A" : "#E60012";
        }

        /// <summary>OPC 断开时重置指示灯为中性灰色</summary>
        private void RefreshParameterColors()
        {
            SpindleStatusColor = "#9E9E9E";
            FeedRateStatusColor = "#9E9E9E";
            TableReadyColor = "#9E9E9E";
            SafetyDoorColor = "#9E9E9E";
        }

        // ═══════════════ 分页 ═══════════════

        [RelayCommand]
        private void PreviousPage()
        {
            if (PageIndex > 0)
            {
                PageIndex--;
                RefreshPagedData();
            }
        }

        [RelayCommand]
        private void NextPage()
        {
            if (PageIndex < TotalPages - 1)
            {
                PageIndex++;
                RefreshPagedData();
            }
        }

        private void RefreshPagedData()
        {
            TotalCount = PlcLineData.Count;
            TotalPages = TotalCount > 0
                ? (int)Math.Ceiling((double)TotalCount / DefaultPageSize)
                : 1;

            CanPreviousPage = PageIndex > 0;
            CanNextPage = PageIndex < TotalPages - 1;

            PagedPlcLineData.Clear();
            var start = PageIndex * DefaultPageSize;
            var end = Math.Min(start + DefaultPageSize, TotalCount);

            for (int i = start; i < end; i++)
            {
                PagedPlcLineData.Add(PlcLineData[i]);
            }
        }

        // ═══════════════ 计时器 ═══════════════
        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_isTimerRunning)
            {
                var elapsed = DateTime.Now - _machiningStartTime;
                MachiningDurationText = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        private void StartTimer()
        {
            _machiningStartTime = DateTime.Now;
            _isTimerRunning = true;
            _machiningTimer.Start();
        }

        private void StopTimer()
        {
            _isTimerRunning = false;
            _machiningTimer.Stop();
        }

        private void ResetTimer()
        {
            StopTimer();
            MachiningDurationText = "00:00:00";
        }

        // ═══════════════ 加工控制命令 ═══════════════

        [RelayCommand]
        private async Task StartAsync()
        {
            if (CurrentWall == null) return;
            try
            {
                await _machiningAppService.StartMachiningAsync(CurrentWall.Id, OperatorName);
                StartTimer();
                UpdateButtonStates(ProcessStatus.加工中);
                SetStatus("加工中", "#4CAF50");
                _logger.LogInformation("开始加工: {WallId}", CurrentWall.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开始加工失败");
                MessageBox.Show($"开始加工失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PauseAsync()
        {
            if (CurrentWall == null) return;
            try
            {
                await _machiningAppService.PauseMachiningAsync(CurrentWall.Id);
                StopTimer();
                UpdateButtonStates(ProcessStatus.暂停);
                SetStatus("暂停", "#FF9800");
                _logger.LogInformation("暂停加工: {WallId}", CurrentWall.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "暂停加工失败");
            }
        }

        [RelayCommand]
        private async Task EmergencyStopAsync()
        {
            if (CurrentWall == null) return;
            try
            {
                await _machiningAppService.EmergencyStopAsync(CurrentWall.Id);
                StopTimer();
                UpdateButtonStates(ProcessStatus.暂停);
                SetStatus("急停", "#F44336");
                _logger.LogWarning("急停: {WallId}", CurrentWall.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "急停失败");
                MessageBox.Show($"急停失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            if (CurrentWall == null) return;
            try
            {
                await _machiningAppService.ResetMachiningAsync(CurrentWall.Id);
                ResetTimer();
                UpdateButtonStates(ProcessStatus.待加工);
                SetStatus("待加工", "#9E9E9E");
                _logger.LogInformation("复位: {WallId}", CurrentWall.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "复位失败");
            }
        }

        [RelayCommand]
        private async Task RegisterExceptionAsync()
        {
            if (CurrentWall == null)
            {
                _logger.LogWarning("异常登记失败：当前无选中墙体");
                MessageBox.Show("请先在左侧加工队列中选择墙体", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 弹出异常登记窗口，不改变墙体加工状态
                OpenMarkExceptionWindow(CurrentWall.WallId, "异常登记");
                _logger.LogInformation("异常登记: {WallId}", CurrentWall.WallId);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "异常登记弹出窗口失败");
                MessageBox.Show($"打开窗口失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task MarkExceptionAsync()
        {
            if (CurrentWall == null) return;
            try
            {
                await _machiningAppService.MarkExceptionAsync(CurrentWall.Id, OperatorName);
                StopTimer();
                UpdateButtonStates(ProcessStatus.中止);
                SetStatus("异常", "#F44336");
                _logger.LogWarning("标记异常: {WallId}", CurrentWall.WallId);

                // 弹出异常登记窗口
                OpenMarkExceptionWindow(CurrentWall.WallId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记异常失败");
            }
        }

        private void OpenMarkExceptionWindow(string wallId, string? title = null)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    // 通过 DI 解析 ViewModel 和 Window
                    var vm = ActivatorUtilities.CreateInstance<MarkExceptionViewModel>(_serviceProvider);
                    vm.Initialize(OperatorName, wallId, title);

                    var window = ActivatorUtilities.CreateInstance<MarkExceptionWindow>(_serviceProvider, vm);
                    window.Owner = Application.Current.MainWindow;
                    window.ShowDialog();

                    _logger.LogInformation("异常登记窗口已关闭");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "打开异常登记窗口失败");
                }
            });
        }

        [RelayCommand]
        private async Task CompleteAsync()
        {
            if (CurrentWall == null) return;

            var result = MessageBox.Show(
                $"确认完成加工？\n墙体：{CurrentWall.WallId}\n操作人：{OperatorName}",
                "确认完成", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _machiningAppService.CompleteMachiningAsync(CurrentWall.Id, OperatorName);
                StopTimer();
                UpdateButtonStates(ProcessStatus.待质检);
                SetStatus("待质检", "#2196F3");
                _logger.LogInformation("完成加工: {WallId}, 进入待质检", CurrentWall.WallId);

                // 刷新队列
                await LoadQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成加工失败");
                MessageBox.Show($"完成加工失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════ 状态管理 ═══════════════
        private void UpdateButtonStates(ProcessStatus status)
        {
            CanStart = status == ProcessStatus.待加工 || status == ProcessStatus.暂停;
            CanPause = status == ProcessStatus.加工中;
            CanEmergencyStop = status == ProcessStatus.加工中;
            CanReset = status == ProcessStatus.暂停 || status == ProcessStatus.中止;
            CanMarkException = status == ProcessStatus.加工中 || status == ProcessStatus.暂停;
            CanComplete = status == ProcessStatus.加工中;
        }

        private void SetStatus(string text, string indicatorColor)
        {
            StatusText = text;
            StatusIndicatorColor = indicatorColor;
            StatusBadgeBackground = GetBadgeBackground(text);
        }

        private static string GetBadgeBackground(string status) => status switch
        {
            "就绪" => "#1677FF",
            "待加工" => "#95A5A6",
            "加工中" => "#90D5FF",
            "暂停" => "#FFD591",
            "急停" => "#FFA39E",
            "异常" => "#FFA39E",
            "中止" => "#FFA39E",
            "待质检" => "#FFD591",
            "已质检" => "#B7EB8F",
            "已完成" => "#B7EB8F",
            _ => "#D9D9D9"
        };

        private static string GetStageColor(int status) => status switch
        {
            2 => "#BAE7FF",  // 加工中 - 浅蓝
            3 => "#FFCCC7",  // 异常 - 浅红
            4 => "#FFE7BA",  // 暂停 - 浅橙
            5 => "#FFCCC7",  // 中止 - 浅红
            6 => "#FFE7BA",  // 待质检 - 浅橙
            7 => "#D9F7BE",  // 已质检 - 浅绿
            8 => "#D9F7BE",  // 已完成 - 浅绿
            _ => "#F5F5F5"
        };

        private void NavigateToExceptionReport(long wallId)
        {
            // 通过 MainPageViewModel 导航到异常报告页面
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var mainWindow = Application.Current?.MainWindow as MainWindow;
                var mainPageHost = mainWindow?.FindName("MainPageHost") as System.Windows.Controls.ContentControl;
                var mainPage = mainPageHost?.Content as System.Windows.FrameworkElement;

                if (mainPage?.DataContext is MainPageViewModel mainPageVm)
                {
                    mainPageVm.AddOrActivateTab("ExceptionReportPage", page =>
                    {
                        if (page is Views.ExceptionReportPage exPage
                            && exPage.DataContext is ExceptionReportPageViewModel exVm)
                        {
                            exVm.OperatorName = OperatorName;
                            _ = exVm.InitializeAsync(wallId, CurrentWall?.WallId ?? "");
                        }
                    });
                }
            });
        }
    }
}
