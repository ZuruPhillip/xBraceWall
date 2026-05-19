using CncWallStation.ViewModels;
using System.Windows.Controls;

namespace CncWallStation.Views
{
    public partial class MainPage : UserControl
    {
        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
