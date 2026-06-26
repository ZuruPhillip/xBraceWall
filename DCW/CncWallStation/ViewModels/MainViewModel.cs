using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Models.Enums;
using CncWallStation.Services.OpcUa;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CncWallStation.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly MainPageViewModel _mainPageViewModel;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IOpcUaService _opcUaService;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string _currentTime = string.Empty;

        [ObservableProperty]
        private string _opcStatusText = "OPC: 未连接";

        [ObservableProperty]
        private string _opcStatusColor = "#9E9E9E";

        public MainViewModel(
            MainPageViewModel mainPageViewModel,
            ILogger<MainViewModel> logger,
            IOpcUaService opcUaService)
        {
            _mainPageViewModel = mainPageViewModel;
            _logger = logger;
            _opcUaService = opcUaService;

            NavigateCommand = new RelayCommand<string>(NavigateToPage);

            // 定时器每秒更新当前时间
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => UpdateTime();
        }

        public ICommand NavigateCommand { get; }

        public void Initialize()
        {
            // 启动时间更新
            UpdateTime();
            _timer.Start();

            // 订阅 OPC 连接状态变更
            _opcUaService.StatusChanged += OnOpcStatusChanged;
            // 初始状态同步
            UpdateOpcStatus(_opcUaService.Status);

            // 默认打开墙体清单选项卡
            NavigateToPage("WallListPage");

            _logger.LogInformation("MainViewModel 初始化完成");
        }

        private void OnOpcStatusChanged(object? sender, OpcConnectionStatus status)
        {
            Application.Current.Dispatcher.BeginInvoke(() => UpdateOpcStatus(status));
        }

        private void UpdateOpcStatus(OpcConnectionStatus status)
        {
            (OpcStatusText, OpcStatusColor) = status switch
            {
                OpcConnectionStatus.Connected => ("OPC: 已连接", "#4CAF50"),
                OpcConnectionStatus.Connecting => ("OPC: 连接中...", "#FF9800"),
                OpcConnectionStatus.Error => ("OPC: 异常", "#F44336"),
                _ => ("OPC: 未连接", "#9E9E9E")
            };
        }

        private void UpdateTime()
        {
            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void NavigateToPage(string pageName)
        {
            _mainPageViewModel.AddOrActivateTab(pageName);
        }
    }
}
