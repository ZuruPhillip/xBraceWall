using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CncWallStation.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainPageViewModel> _logger;

        private readonly Dictionary<string, (string Header, Type PageType)> _pageMap = new()
        {
            { "WallListPage", ("墙体清单", typeof(Views.WallListPage)) },
            { "BimDataRenderPage", ("BIM模型渲染", typeof(Views.BimDataRenderPage)) },
            { "MomDataRenderPage", ("MOM模型渲染", typeof(Views.MomDataRenderPage)) },
            { "ControllerPage", ("控制页面", typeof(Views.ControllerPage)) },
            { "JsonEditPage", ("JSON编辑器", typeof(Views.JsonEditPage)) }
        };

        public ObservableCollection<TabItemViewModel> Tabs { get; } = new();

        [ObservableProperty]
        private TabItemViewModel? _selectedTab;

        public MainPageViewModel(
            IServiceProvider serviceProvider,
            ILogger<MainPageViewModel> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void AddOrActivateTab(string pageKey)
        {
            if (!_pageMap.TryGetValue(pageKey, out var pageInfo))
            {
                _logger.LogError("未找到页面: {PageKey}", pageKey);
                return;
            }

            // 检查是否已打开
            var existingTab = Tabs.FirstOrDefault(t => t.PageKey == pageKey);
            if (existingTab != null)
            {
                existingTab.IsSelected = true;
                SelectedTab = existingTab;
                _logger.LogInformation("激活已有选项卡: {PageKey}", pageKey);
                return;
            }

            // 创建新选项卡
            var page = _serviceProvider.GetRequiredService(pageInfo.PageType) as Page;
            var frame = new Frame
            {
                Content = page,
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };
            var tab = new TabItemViewModel(pageInfo.Header, pageKey, frame)
            {
                IsSelected = true
            };

            Tabs.Add(tab);
            SelectedTab = tab;
            _logger.LogInformation("新建选项卡: {PageKey}", pageKey);
        }

        [RelayCommand]
        private void CloseTab(TabItemViewModel? tab)
        {
            if (tab == null) return;

            Tabs.Remove(tab);
            _logger.LogInformation("关闭选项卡: {PageKey}", tab.PageKey);

            // 切换到最后一个选项卡
            if (Tabs.Count > 0)
            {
                var lastTab = Tabs[^1];
                lastTab.IsSelected = true;
                SelectedTab = lastTab;
            }
            else
            {
                SelectedTab = null;
            }
        }
    }
}
