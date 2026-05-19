using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;

namespace CncWallStation.ViewModels
{
    public partial class TabItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _pageKey = string.Empty;

        [ObservableProperty]
        private object? _content;

        [ObservableProperty]
        private bool _isSelected;

        public TabItemViewModel(string header, string pageKey, object? content)
        {
            _header = header;
            _pageKey = pageKey;
            _content = content;
        }
    }
}
