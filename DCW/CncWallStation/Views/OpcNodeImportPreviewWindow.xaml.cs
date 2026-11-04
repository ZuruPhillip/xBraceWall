using System.Windows;
using CncWallStation.ViewModels;

namespace CncWallStation.Views
{
    /// <summary>
    /// Excel 导入预览弹窗
    /// </summary>
    public partial class OpcNodeImportPreviewWindow : Window
    {
        public OpcNodeImportPreviewWindow(OpcNodeImportPreviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
