using System.Windows;
using CncWallStation.ViewModels;

namespace CncWallStation.Views
{
    /// <summary>
    /// 新建 / 重命名 OPC 节点分组弹窗
    /// </summary>
    public partial class OpcNodeGroupEditWindow : Window
    {
        public OpcNodeGroupEditWindow(OpcNodeGroupEditViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
