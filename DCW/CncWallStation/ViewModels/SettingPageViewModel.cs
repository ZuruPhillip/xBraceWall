using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;

namespace CncWallStation.ViewModels;

public partial class SettingPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isChinese = true;

    [ObservableProperty]
    private bool _isEnglish = false;

    public SettingPageViewModel()
    {
        var lang = LocalizationService.Instance.CurrentLanguage;
        if (lang == "zh-CN")
        {
            _isChinese = true;
            _isEnglish = false;
        }
        else
        {
            _isChinese = false;
            _isEnglish = true;
        }
    }

    [RelayCommand]
    private void SwitchToChinese()
    {
        if (IsChinese) return;
        IsChinese = true;
        IsEnglish = false;
        LocalizationService.Instance.SetCulture("zh-CN");
    }

    [RelayCommand]
    private void SwitchToEnglish()
    {
        if (IsEnglish) return;
        IsChinese = false;
        IsEnglish = true;
        LocalizationService.Instance.SetCulture("en-US");
    }
}
