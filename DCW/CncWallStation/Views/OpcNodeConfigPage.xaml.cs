using CncWallStation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CncWallStation.Views
{
    /// <summary>
    /// 【OPC节点配置】页面：分组管理 + 节点查询/分页 + 增删改弹窗 + Excel 导入导出
    /// </summary>
    public partial class OpcNodeConfigPage : Page
    {
        private readonly OpcNodeConfigViewModel _viewModel;
        private bool _initialized;

        public OpcNodeConfigPage(OpcNodeConfigViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += OnPageLoaded;
            IsVisibleChanged += OnPageVisibleChanged;
            Unloaded += OnPageUnloaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _initialized = true;
            await _viewModel.InitializeAsync();
        }

        /// <summary>页签切换回本页时刷新，保证其他页面修改的节点状态能及时反映</summary>
        private async void OnPageVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_initialized && e.NewValue is true)
                await _viewModel.OnActivatedAsync();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Dispose();
        }

        /// <summary>关键字输入框回车即触发查询</summary>
        private void OnKeywordKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            _viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
