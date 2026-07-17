using CncWallStation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    public partial class ControllerPage : Page
    {
        private readonly ControllerPageViewModel _viewModel;
        private bool _initialized;

        public ControllerPage(ControllerPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += OnPageLoaded;
            IsVisibleChanged += OnPageVisibleChanged;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            await _viewModel.InitializeAsync();
        }

        /// <summary>
        /// 页面切换回控制页签时重新加载实时参数节点，
        /// 确保用户在系统设置中的最新勾选即时同步。
        /// </summary>
        private async void OnPageVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_initialized && (bool)e.NewValue)
            {
                await _viewModel.ReloadRealtimeNodesAsync();
            }
        }
    }
}
