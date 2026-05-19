using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using System.Windows.Threading;

namespace CncWallStation.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly MainPageViewModel _mainPageViewModel;
        private readonly ILogger<MainViewModel> _logger;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string _currentTime = string.Empty;

        public MainViewModel(
            MainPageViewModel mainPageViewModel,
            ILogger<MainViewModel> logger)
        {
            _mainPageViewModel = mainPageViewModel;
            _logger = logger;

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

            // 默认打开墙体清单选项卡
            NavigateToPage("WallListPage");

            _logger.LogInformation("MainViewModel 初始化完成");
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