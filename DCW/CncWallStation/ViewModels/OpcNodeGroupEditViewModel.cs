using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 新建 / 重命名 OPC 节点分组弹窗 ViewModel
    /// </summary>
    public partial class OpcNodeGroupEditViewModel : ObservableObject
    {
        /// <summary>分组名称最大长度（与表结构一致）</summary>
        private const int MaxGroupNameLength = 64;

        private readonly Func<string, Task<bool>>? _nameExistsCheck;

        public OpcNodeGroupEditViewModel(
            OpcNodeGroupItem? group = null,
            Func<string, Task<bool>>? nameExistsCheck = null)
        {
            _nameExistsCheck = nameExistsCheck;
            IsEditMode = group != null;
            Title = IsEditMode
                ? LocalizationService.Instance["OpcNode_RenameGroupTitle"]
                : LocalizationService.Instance["OpcNode_NewGroupTitle"];

            if (group != null)
            {
                Name = group.Name;
                Description = string.Empty;
            }
        }

        public bool IsEditMode { get; }

        public string Title { get; }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private int _nameLength;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary>字数提示文本，如 “12 / 64”</summary>
        public string NameLengthText => $"{NameLength} / {MaxGroupNameLength}";

        partial void OnNameChanged(string value)
        {
            NameLength = value?.Length ?? 0;
            OnPropertyChanged(nameof(NameLengthText));
        }

        partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

        /// <summary>true=保存，false=取消</summary>
        public event EventHandler<bool>? CloseRequested;

        [RelayCommand]
        private async Task SaveAsync()
        {
            ErrorMessage = null;

            var name = Name?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                ErrorMessage = LocalizationService.Instance["OpcNode_GroupNameRequired"];
                return;
            }

            if (name.Length > MaxGroupNameLength)
            {
                ErrorMessage = string.Format(
                    LocalizationService.Instance["OpcNode_GroupNameTooLong"], MaxGroupNameLength);
                return;
            }

            if (_nameExistsCheck != null && await _nameExistsCheck(name))
            {
                ErrorMessage = LocalizationService.Instance["OpcNode_GroupNameExists"];
                return;
            }

            CloseRequested?.Invoke(this, true);
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, false);
    }
}
