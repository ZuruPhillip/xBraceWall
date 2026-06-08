using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;

namespace CncWallStation.ViewModels
{
    public partial class TabItemViewModel : ObservableObject
    {
        private readonly string _headerKey = string.Empty;

        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _pageKey = string.Empty;

        [ObservableProperty]
        private object? _content;

        [ObservableProperty]
        private bool _isSelected;

        public TabItemViewModel(string headerKey, string pageKey, object? content)
        {
            _headerKey = headerKey;
            _pageKey = pageKey;
            _content = content;
            RefreshHeader();

            Localization.LocalizationService.Instance.CultureChanged += OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, string e)
        {
            RefreshHeader();
        }

        private void RefreshHeader()
        {
            Header = Localization.LocalizationService.Instance[_headerKey];
        }

        /// <summary>
        /// 清理事件订阅，防止内存泄漏
        /// </summary>
        public void Dispose()
        {
            Localization.LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        }
    }
}
