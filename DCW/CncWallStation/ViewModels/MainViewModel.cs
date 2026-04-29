using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace CncWallStation.ViewModels
{
    public class MainViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainViewModel> _logger;

        private Frame? _mainFrame;

        // 页面映射表
        private readonly Dictionary<string, Type> _pageMap = new()
        {
            { "ControllerPage", typeof(Views.ControllerPage) },
            { "BimDataRenderPage", typeof(Views.BimDataRenderPage) }
        };

        public MainViewModel(
            IServiceProvider serviceProvider,
            ILogger<MainViewModel> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            NavigateCommand = new RelayCommand<string>(NavigateToPage);
        }

        public void SetFrame(Frame frame)
        {
            _mainFrame = frame;
        }

        public ICommand NavigateCommand { get; }

        private void NavigateToPage(string pageName)
        {
            if (_mainFrame == null)
            {
                _logger.LogWarning("MainFrame 未初始化");
                return;
            }

            if (!_pageMap.TryGetValue(pageName, out var pageType))
            {
                _logger.LogError("未找到页面: {PageName}", pageName);
                return;
            }

            var page = _serviceProvider.GetRequiredService(pageType);
            _mainFrame.Navigate(page);

            _logger.LogInformation("导航到页面: {PageName}", pageName);
        }
    }
}