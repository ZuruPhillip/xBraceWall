using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;
using CncWallStation.Services.OpcUa;
using System.Collections.ObjectModel;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 新建 / 编辑 OPC 节点弹窗 ViewModel
    /// </summary>
    public partial class OpcNodeEditViewModel : ObservableObject
    {
        /// <summary>NodeId 最大长度（与表结构一致）</summary>
        private const int MaxNodeIdLength = 256;

        private readonly Func<string, Task<bool>>? _nodeIdExistsCheck;

        /// <summary>NodeId 存在性校验（由宿主页面注入，避免 VM 直接依赖数据服务）</summary>
        public OpcNodeEditViewModel(
            IEnumerable<OpcNodeGroupItem> groups,
            OpcNodeConfig? node,
            Func<string, Task<bool>>? nodeIdExistsCheck = null)
        {
            _nodeIdExistsCheck = nodeIdExistsCheck;

            GroupOptions = new ObservableCollection<OpcNodeGroupItem>(groups);
            // 首位固定为“未分组”，方便把节点移出分组
            GroupOptions.Insert(0, new OpcNodeGroupItem(null, false, LocalizationService.Instance["OpcNode_Ungrouped"]));

            IsEditMode = node != null;
            Title = IsEditMode
                ? LocalizationService.Instance["OpcNode_EditNodeTitle"]
                : LocalizationService.Instance["OpcNode_NewNodeTitle"];

            if (node != null)
            {
                NodeId = node.NodeId;
                Description = node.Description;
                IsWritable = node.IsWritable;
                IsShowInRealtime = node.IsShowInRealtime;
                SelectedGroupOption = GroupOptions.FirstOrDefault(
                    g => g.GroupId == node.GroupId && !g.IsAll) ?? GroupOptions[0];
            }
            else
            {
                SelectedGroupOption = GroupOptions[0];
            }
        }

        public ObservableCollection<OpcNodeGroupItem> GroupOptions { get; }

        public bool IsEditMode { get; }

        /// <summary>新建模式（NodeId 可输入）</summary>
        public bool IsCreateMode => !IsEditMode;

        public string Title { get; }

        /// <summary>编辑模式提示：NodeId 作为订阅键不可修改</summary>
        public string NodeIdHint => LocalizationService.Instance["OpcNode_NodeIdEditHint"];

        [ObservableProperty]
        private string _nodeId = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private OpcNodeGroupItem? _selectedGroupOption;

        [ObservableProperty]
        private bool _isWritable;

        [ObservableProperty]
        private bool _isShowInRealtime;

        [ObservableProperty]
        private string? _errorMessage;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

        /// <summary>true=保存，false=取消</summary>
        public event EventHandler<bool>? CloseRequested;

        [RelayCommand]
        private async Task SaveAsync()
        {
            ErrorMessage = null;

            var nodeId = NodeId?.Trim() ?? string.Empty;

            if (!IsEditMode)
            {
                if (nodeId.Length == 0)
                {
                    ErrorMessage = LocalizationService.Instance["OpcNode_NodeIdRequired"];
                    return;
                }

                if (nodeId.Length > MaxNodeIdLength)
                {
                    ErrorMessage = string.Format(
                        LocalizationService.Instance["OpcNode_NodeIdTooLong"], MaxNodeIdLength);
                    return;
                }

                if (_nodeIdExistsCheck != null && await _nodeIdExistsCheck(nodeId))
                {
                    ErrorMessage = LocalizationService.Instance["OpcNode_NodeIdExists"];
                    return;
                }
            }

            CloseRequested?.Invoke(this, true);
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, false);
    }
}
