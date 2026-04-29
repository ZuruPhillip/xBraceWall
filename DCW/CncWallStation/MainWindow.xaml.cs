using CncWallStation.ViewModels;
using CncWallStation.Views;
using System.Windows;

namespace CncWallStation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel mainViewModel, ControllerPage controllerPage)
        {
            InitializeComponent();
            DataContext = mainViewModel;
            mainViewModel.SetFrame(MainFrame);
            MainFrame.Navigate(controllerPage);
        }
    }
}