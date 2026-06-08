using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CncWallStation.Localization
{
    /// <summary>
    /// 运行时动态本地化服务（单例），管理语言切换和字符串资源检索。
    /// 通过 INotifyPropertyChanged 通知 WPF 绑定刷新。
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly ResourceManager _resourceManager;
    private readonly string _prefsPath;
    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>
    /// 当前语言标识：zh-CN 或 en-US
    /// </summary>
    public string CurrentLanguage => _currentCulture.Name;

    /// <summary>
    /// 当前语言显示名称（中文用"中文"，英文用"English"）
    /// </summary>
    public string CurrentLanguageDisplay =>
        _currentCulture.Name.StartsWith("zh") ? "中文" : "English";

    /// <summary>
    /// 字符串索引器，供 XAML 的 Binding 路径 "Item[Key]" 使用。
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? $"#{key}#";
        }
    }

    /// <summary>
    /// 切换语言并通知所有绑定刷新。
    /// </summary>
    /// <param name="cultureName">zh-CN 或 en-US</param>
    public void SetCulture(string cultureName)
    {
        if (_currentCulture.Name == cultureName)
            return;

        _currentCulture = CultureInfo.GetCultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = _currentCulture;
        Thread.CurrentThread.CurrentUICulture = _currentCulture;

        SavePreference();

        // 通知 WPF 绑定：索引器变更
        OnPropertyChanged(BindingIndexerName);
        // 通知代码订阅者
        CultureChanged?.Invoke(this, cultureName);

        System.Diagnostics.Debug.WriteLine($"[LocalizationService] 语言已切换为: {cultureName}");
    }

    /// <summary>
    /// 从本地偏好文件加载上次保存的语言。
    /// </summary>
    public void LoadSavedLanguage()
    {
        try
        {
            if (File.Exists(_prefsPath))
            {
                var json = File.ReadAllText(_prefsPath);
                var prefs = JsonSerializer.Deserialize<LangPrefs>(json);
                if (prefs?.Language is "zh-CN" or "en-US")
                {
                    _currentCulture = CultureInfo.GetCultureInfo(prefs.Language);
                }
            }
        }
        catch
        {
            _currentCulture = CultureInfo.GetCultureInfo("zh-CN");
        }

        Thread.CurrentThread.CurrentCulture = _currentCulture;
        Thread.CurrentThread.CurrentUICulture = _currentCulture;

        // 通知默认语言（不触发 CultureChanged，因为这是初始化阶段）
        OnPropertyChanged(BindingIndexerName);
    }

    private void SavePreference()
    {
        try
        {
            var prefs = new LangPrefs { Language = _currentCulture.Name };
            var dir = Path.GetDirectoryName(_prefsPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_prefsPath, JsonSerializer.Serialize(prefs));
        }
        catch
        {
            // 静默失败，不影响主流程
        }
    }

    private LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "CncWallStation.Resources.Strings",
            Assembly.GetExecutingAssembly());
        _prefsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CncWallStation", "lang_prefs.json");
    }

    /// <summary>
    /// 在 WPF Binding 中用于通知索引器变更的特殊属性名。
    /// </summary>
    public const string BindingIndexerName = "Item[]";

    /// <summary>
    /// 当文化变更时触发，用于代码订阅（如 WebView2 同步调用 setLanguage）。
    /// </summary>
    public event EventHandler<string>? CultureChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private class LangPrefs
    {
        public string Language { get; set; } = "zh-CN";
    }
    }
}
