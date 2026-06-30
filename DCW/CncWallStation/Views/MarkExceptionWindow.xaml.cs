using CncWallStation.ViewModels;
using System.Windows;

namespace CncWallStation.Views
{
    public partial class MarkExceptionWindow : Window
    {
        public MarkExceptionWindow(MarkExceptionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
