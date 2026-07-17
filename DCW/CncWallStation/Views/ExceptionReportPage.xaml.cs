using CncWallStation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    public partial class ExceptionReportPage : Page
    {
        private readonly ExceptionReportPageViewModel _viewModel;

        public ExceptionReportPage(ExceptionReportPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 页面首次加载时自动初始化数据（避免依赖外部调用 InitializeAsync）
            await _viewModel.EnsureInitializedAsync();
        }
    }
}
