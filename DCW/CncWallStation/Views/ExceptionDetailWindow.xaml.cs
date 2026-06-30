using CncWallStation.ViewModels;

namespace CncWallStation.Views
{
    public partial class ExceptionDetailWindow : System.Windows.Window
    {
        public ExceptionDetailWindow(ExceptionDetailViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
