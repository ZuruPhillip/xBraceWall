using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace CncWallStation.ViewModels
{
    public class MainViewModel
    {
        private Frame _mainFrame;

        public MainViewModel(Frame mainFrame)
        {
            _mainFrame = mainFrame;
            _mainFrame.Navigate(new Uri("Views/BimDataRenderPage.xaml", UriKind.Relative));
            // 初始化导航命令
            NavigateCommand = new RelayCommand<string>(NavigateToPage);
        }

        public ICommand NavigateCommand { get; }

        private void NavigateToPage(string pageName)
        {
            if (_mainFrame == null) return;

            // 根据页面名称导航到对应页面
            switch (pageName)
            {
                case "BimDataRenderPage":
                _mainFrame.Navigate(new Uri("Views/BimDataRenderPage.xaml", UriKind.Relative));
                break;
                //case "ControllerPage":
                //_mainFrame.Navigate(new Uri("Pages/ControllerPage.xaml", UriKind.Relative));
                //break;
                //case "ReportPage":
                //_mainFrame.Navigate(new Uri("Pages/ReportPage.xaml", UriKind.Relative));
                //break;
                //case "SettingPage":
                //_mainFrame.Navigate(new Uri("Pages/SettingPage.xaml", UriKind.Relative));
                //break;
                default:
                throw new ArgumentException($"未找到页面: {pageName}");
            }
        }
    }
}
