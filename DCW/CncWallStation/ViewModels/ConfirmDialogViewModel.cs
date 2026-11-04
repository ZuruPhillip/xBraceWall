using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 通用确认弹窗 ViewModel（删除二次确认等场景）
    /// </summary>
    public partial class ConfirmDialogViewModel : ObservableObject
    {
        public ConfirmDialogViewModel(
            string title,
            string message,
            string confirmText,
            string cancelText,
            bool isDanger = true,
            string? detail = null)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            IsDanger = isDanger;
            Detail = detail;
        }

        public string Title { get; }

        public string Message { get; }

        /// <summary>补充说明（如 NodeId、不可撤销提示）</summary>
        public string? Detail { get; }

        public string ConfirmText { get; }

        public string CancelText { get; }

        /// <summary>危险操作使用红色主按钮</summary>
        public bool IsDanger { get; }

        public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

        /// <summary>true=确认，false=取消</summary>
        public event EventHandler<bool>? CloseRequested;

        [RelayCommand]
        private void Confirm() => CloseRequested?.Invoke(this, true);

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, false);
    }
}
