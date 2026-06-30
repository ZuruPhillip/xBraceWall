using CncWallStation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    public partial class ControllerPage : Page
    {
        private readonly ControllerPageViewModel _viewModel;

        public ControllerPage(ControllerPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }
    }
}
