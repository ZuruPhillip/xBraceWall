using System.Windows;
using CncWallStation.ViewModels;

namespace CncWallStation.Views
{
    /// <summary>
    /// 新建 / 编辑 OPC 节点弹窗
    /// </summary>
    public partial class OpcNodeEditWindow : Window
    {
        public OpcNodeEditWindow(OpcNodeEditViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
