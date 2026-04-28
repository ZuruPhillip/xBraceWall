using CncWallStation.Views;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CncWallStation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 导航到 BimDataRenderPage 作为主页面
            MainFrame.Navigate(new BimDataRenderPage());
            // 将 MainFrame 传递给 MainViewModel
            //DataContext = new MainViewModel(MainFrame);
        }
    }
}