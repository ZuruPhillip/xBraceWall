using System.Windows;
using CncWallStation.ViewModels;

namespace CncWallStation.Views
{
    /// <summary>
    /// 通用确认弹窗（删除等二次确认场景）
    /// </summary>
    public partial class ConfirmDialogWindow : Window
    {
        public ConfirmDialogWindow(ConfirmDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
            Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
        }

        private void OnCloseRequested(object? sender, bool result)
        {
            DialogResult = result;
        }

        /// <summary>
        /// 以模态方式显示确认弹窗
        /// </summary>
        /// <returns>true 表示用户确认</returns>
        public static bool Show(
            string title,
            string message,
            string confirmText,
            string cancelText,
            bool isDanger = true,
            string? detail = null)
        {
            var viewModel = new ConfirmDialogViewModel(title, message, confirmText, cancelText, isDanger, detail);
            var window = new ConfirmDialogWindow(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            return window.ShowDialog() == true;
        }
    }
}
