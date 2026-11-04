using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;
using System.Collections.ObjectModel;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// Excel 导入预览弹窗 ViewModel —— 展示新增/更新/跳过统计与逐行错误明细
    /// </summary>
    public partial class OpcNodeImportPreviewViewModel : ObservableObject
    {
        public OpcNodeImportPreviewViewModel(
            string filePath,
            int addedCount,
            int updatedCount,
            int skippedCount,
            IEnumerable<string>? errors = null)
        {
            FilePath = filePath;
            AddedCount = addedCount;
            UpdatedCount = updatedCount;
            SkippedCount = skippedCount;

            if (errors != null)
            {
                foreach (var error in errors)
                    Errors.Add(error);
            }
        }

        public string FilePath { get; }

        public int AddedCount { get; }

        public int UpdatedCount { get; }

        public int SkippedCount { get; }

        public ObservableCollection<string> Errors { get; } = new();

        public bool HasSkipped => SkippedCount > 0;

        public bool HasErrors => Errors.Count > 0;

        /// <summary>标题：导入预览</summary>
        public string Title => LocalizationService.Instance["OpcNode_ImportPreviewTitle"];

        /// <summary>跳过明细标题，如 “跳过明细（3 条，最多显示前 20 条）”</summary>
        public string ErrorHeader => string.Format(
            LocalizationService.Instance["OpcNode_ImportErrorsFormat"], SkippedCount);

        /// <summary>true=确认导入，false=取消</summary>
        public event EventHandler<bool>? CloseRequested;

        [RelayCommand]
        private void Confirm() => CloseRequested?.Invoke(this, true);

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, false);
    }
}
