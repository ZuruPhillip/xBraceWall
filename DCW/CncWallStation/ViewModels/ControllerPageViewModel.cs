using CncWallStation.Models;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Enums;
using CncWallStation.Services.Application;
using CncWallStation.Services.OpcUa;
using CncWallStation.Views;
using Opc.Ua;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CncWallStation.ViewModels
{
    public partial class ControllerPageViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<ControllerPageViewModel> _logger;
        private readonly IOpcUaService _opcUaService;
        private readonly IMachiningAppService _machiningAppService;
        private readonly IServiceProvider _serviceProvider;

        // 计时器
        private readonly DispatcherTimer _machiningTimer;
        private DateTime _machiningStartTime;
        private bool _isTimerRunning;

        // 释放标志
        private bool _disposed;

        // OPC 节点配置文件路径（与 SettingPageViewModel 共用同一文件）
        private static readonly string NodesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CncWallStation", "opc_nodes.json");

        // 当前墙体数据
        [ObservableProperty] private WallInfoDto? _currentWall;
        [ObservableProperty] private WallQueueItemDto? _selectedQueueItem;

        // 队列
        public ObservableCollection<WallQueueItemDto> WallQueue { get; } = new();

        // 实时参数（用户在系统设置中勾选的节点，动态展示）
        public ObservableCollection<RealtimeNodeItemDto> RealtimeNodes { get; } = new();
        private readonly Dictionary<string, RealtimeNodeItemDto> _realtimeNodeMap = new();

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
                // 加载用户在系统设置中勾选的实时参数节点
                LoadRealtimeNodesFromFile();

                // 若 OPC 已连接，立即订阅实时参数节点（覆盖"先连接后打开控制页"的场景）
                if (_opcUaService.IsConnected)
                {
                    await SubscribeRealtimeNodesAsync();
                }

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

        private const int DefaultPlcLineCount = 30;

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
            //if (!_opcUaService.IsConnected) return;
            try
            {
                var nodes = new System.Collections.Generic.List<OpcNodeConfig>();
                string[] headers = { "T", "F", "D", "X[0]", "Y[0]", "Z[0]", "X[1]", "Y[1]", "Z[1]" };

                for (int i = 0; i < count; i++)
                {
                    foreach (var h in headers)
                    {
                        nodes.Add(new OpcNodeConfig
                        {
                            NodeId = $"ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.Line_Def[{i}].{h}",
                            Description = $"L{i}.{h}"
                        });
                    }
                }

                // 即使 OPC 未连接也调用 SubscribeNodesAsync —— 它会暂存节点，
                // 在连接成功后由 RestoreSubscriptionsInternal 自动恢复订阅
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
                    UpdateRealtimeNodesFromOpc(nodes);
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
                if (IsOpcConnected)
                {
                    // 连接成功后，如果 PLC 数据已初始化则自动恢复订阅
                    if (PlcLineData.Count > 0)
                        _ = SubscribePlcNodesAsync(PlcLineData.Count);
                    // 订阅用户勾选的实时参数节点
                    _ = SubscribeRealtimeNodesAsync();
                }
                else
                {
                    // 断开时重置所有实时参数指示灯为中性灰色
                    foreach (var dto in RealtimeNodes)
                    {
                        dto.DisplayValue = "--";
                        dto.QualityColor = "#9E9E9E";
                    }
                }
            });
        }

        private void UpdatePlcLineDataFromOpc(System.Collections.Generic.IReadOnlyList<OpcNodeConfig> nodes)
        {
            foreach (var node in nodes)
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(node.NodeId,
                        @"Line_Def\[(\d+)\]\.(\w+(?:\[\d+\])?)");
                    if (!match.Success) continue;

                    if (!int.TryParse(match.Groups[1].Value, out int index)) continue;
                    string header = match.Groups[2].Value;

                    if (index < 0 || index >= PlcLineData.Count) continue;

                    var line = PlcLineData[index];
                    var val = System.Convert.ToSingle(node.CurrentValue ?? 0f);

                    switch (header)
                    {
                        case "T": line.T = (int)val; break;
                        case "F": line.F = (int)val; break;
                        case "D": line.D = (int)val; break;
                        case "X[0]": line.X0 = val; break;
                        case "Y[0]": line.Y0 = val; break;
                        case "Z[0]": line.Z0 = val; break;
                        case "X[1]": line.X1 = val; break;
                        case "Y[1]": line.Y1 = val; break;
                        case "Z[1]": line.Z1 = val; break;
                    }

                    // D=1 表示该行已加工完成（双向赋值，支持复位回退）
                    if (header == "D")
                        line.IsCompleted = (int)val == 1;
                }
                catch (Exception ex)
                {
                    // 单个节点解析失败不中断整批更新
                    _logger.LogDebug(ex, "解析 PLC 节点值失败: {NodeId}", node.NodeId);
                }
            }

            // ★ 基于全部行的累计完成状态计算进度，避免因批量聚合只含部分节点而乱跳/倒退
            if (PlcLineData.Count > 0)
            {
                var completedCount = PlcLineData.Count(l => l.IsCompleted);
                var pct = completedCount * 100 / PlcLineData.Count;
                ProgressText = $"{pct}%";
            }
        }

        /// <summary>根据 OPC 订阅回调更新实时参数节点显示值</summary>
        private void UpdateRealtimeNodesFromOpc(System.Collections.Generic.IReadOnlyList<OpcNodeConfig> nodes)
        {
            foreach (var node in nodes)
            {
                if (_realtimeNodeMap.TryGetValue(node.NodeId, out var dto))
                {
                    dto.DisplayValue = node.CurrentValue?.ToString() ?? "--";
                    dto.QualityColor = node.Quality == "Good" ? "#52C41A" : "#E60012";
                }
            }
        }

        /// <summary>从 opc_nodes.json 加载用户勾选了"实时显示"的节点到面板</summary>
        /// <summary>
        /// 重新从配置文件加载实时参数节点并订阅。
        /// 供页面切换到控制页签时调用，确保用户在系统设置中的最新勾选同步生效。
        /// </summary>
        public async Task ReloadRealtimeNodesAsync()
        {
            LoadRealtimeNodesFromFile();
            await SubscribeRealtimeNodesAsync();
        }

        private void LoadRealtimeNodesFromFile()
        {
            RealtimeNodes.Clear();
            _realtimeNodeMap.Clear();

            try
            {
                if (!File.Exists(NodesFilePath)) return;

                var json = File.ReadAllText(NodesFilePath);
                var nodes = JsonSerializer.Deserialize<List<OpcNodeConfig>>(json);
                if (nodes == null) return;

                foreach (var node in nodes.Where(n => n.IsShowInRealtime))
                {
                    var dto = new RealtimeNodeItemDto
                    {
                        NodeId = node.NodeId,
                        Description = string.IsNullOrWhiteSpace(node.Description) ? node.NodeId : node.Description
                    };
                    RealtimeNodes.Add(dto);
                    _realtimeNodeMap[node.NodeId] = dto;
                }

                _logger.LogInformation("加载实时参数节点: {Count} 个", RealtimeNodes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载实时参数节点失败");
            }
        }

        /// <summary>订阅用户勾选的实时参数节点（OPC 连接后调用）</summary>
        private async Task SubscribeRealtimeNodesAsync()
        {
            if (_realtimeNodeMap.Count == 0) return;

            try
            {
                var nodeIds = _realtimeNodeMap.Keys.ToList();
                var nodes = nodeIds.Select(id => new OpcNodeConfig { NodeId = id }).ToList();

                // 即使 OPC 未连接也调用 SubscribeNodesAsync —— 它会暂存节点，
                // 在连接成功后由 RestoreSubscriptionsInternal 自动恢复订阅
                await _opcUaService.SubscribeNodesAsync(nodes);
                _logger.LogInformation("已订阅 {Count} 个实时参数节点", nodes.Count);

                // 若已连接，立即读取一次当前值，避免等待订阅回调延迟
                if (_opcUaService.IsConnected)
                {
                    var values = await _opcUaService.ReadNodesAsync(nodeIds);
                    for (int i = 0; i < nodeIds.Count && i < values.Count; i++)
                    {
                        if (_realtimeNodeMap.TryGetValue(nodeIds[i], out var dto))
                        {
                            dto.DisplayValue = values[i].Value?.ToString() ?? "--";
                            dto.QualityColor = StatusCode.IsGood(values[i].StatusCode) ? "#52C41A" : "#E60012";
                        }
                    }
                    _logger.LogDebug("已读取 {Count} 个实时参数节点初始值", nodeIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "订阅实时参数节点失败");
            }
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
                            exVm.RegistrantName = OperatorName;
                            _ = exVm.InitializeAsync(wallId, CurrentWall?.WallId ?? "");
                        }
                    });
                }
            });
        }

        // ═══════════════ 释放 ═══════════════
        /// <summary>
        /// 解绑 OPC 单例事件与计时器，避免单例长期持有本 ViewModel 引用导致的
        /// 内存泄漏与「幽灵回调」（旧页面实例仍被 OPC 数据更新触发）。
        /// 页面关闭 / 导航移除时务必调用。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 解绑 OPC 事件（关键：单例服务不再持有本实例）
            _opcUaService.NodeValuesUpdated -= OnOpcNodeValuesUpdated;
            _opcUaService.StatusChanged -= OnOpcStatusChanged;

            // 停止并解绑计时器
            _machiningTimer.Stop();
            _machiningTimer.Tick -= OnTimerTick;

            GC.SuppressFinalize(this);
        }
    }
}