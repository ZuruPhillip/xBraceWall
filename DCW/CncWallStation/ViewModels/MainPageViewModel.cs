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

        private readonly Dictionary<string, (string HeaderKey, Type PageType)> _pageMap = new()
        {
            { "WallListPage", ("TabHeader_WallList", typeof(Views.WallListPage)) },
            { "BimDataRenderPage", ("TabHeader_BimRender", typeof(Views.BimDataRenderPage)) },
            { "MomDataRenderPage", ("TabHeader_MomRender", typeof(Views.MomDataRenderPage)) },
            { "ControllerPage", ("TabHeader_Controller", typeof(Views.ControllerPage)) },
            { "JsonEditPage", ("TabHeader_JsonEditor", typeof(Views.JsonEditPage)) },
            { "DataCheckPage", ("TabHeader_DataCheck", typeof(Views.DataCheckPage)) },
            { "PlcDataPage", ("TabHeader_PlcData", typeof(Views.PlcDataPage)) },
            { "SettingPage", ("TabHeader_Settings", typeof(Views.SettingPage)) }
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

        public void AddOrActivateTab(string pageKey, Action<Page>? onPageCreated = null)
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

                // 对已有页面也触发回调（用于更新 WallId 等参数）
                if (existingTab.Content is Frame { Content: Page existingPage })
                    onPageCreated?.Invoke(existingPage);

                _logger.LogInformation("激活已有选项卡: {PageKey}", pageKey);
                return;
            }

            // 创建新选项卡
            var page = _serviceProvider.GetRequiredService(pageInfo.PageType) as Page;
            if (page == null)
            {
                _logger.LogError("无法创建页面: {PageKey}", pageKey);
                return;
            }

            onPageCreated?.Invoke(page);

            var frame = new Frame
            {
                Content = page,
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };
            var tab = new TabItemViewModel(pageInfo.HeaderKey, pageKey, frame)
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
            tab.Dispose();
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
