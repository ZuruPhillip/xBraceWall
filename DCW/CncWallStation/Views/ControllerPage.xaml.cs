using CncWallStation.ViewModels;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    /// <summary>
    /// ControllerPage.xaml 的交互逻辑
    /// </summary>
    public partial class ControllerPage : Page
    {
        private readonly ControllerPageViewModel _viewModel;
        public ControllerPage(ControllerPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
    }
}
