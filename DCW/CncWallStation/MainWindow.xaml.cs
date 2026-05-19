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
        public MainWindow(MainViewModel mainViewModel, MainPage mainPage)
        {
            InitializeComponent();
            DataContext = mainViewModel;

            // 通过 DI 注入 MainPage，避免 XAML 无参构造函数问题
            MainPageHost.Content = mainPage;

            mainViewModel.Initialize();
        }
    }
}