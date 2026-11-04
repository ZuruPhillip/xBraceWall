using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;
using CncWallStation.Services.OpcUa;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CncWallStation.ViewModels;

/// <summary>
/// 系统设置页 ViewModel：语言设置 + OPC 连接配置。
/// 说明：OPC 节点管理已独立到【OPC节点配置】页（OpcNodeConfigPage），本页不再持有节点列表。
/// </summary>
public partial class SettingPageViewModel : ObservableObject, IDisposable
{
    private readonly IOpcUaService _opcUaService = null!;
    private readonly ILogger<SettingPageViewModel> _logger = null!;
    private bool _disposed;

    [ObservableProperty]
    private bool _isChinese = true;

    [ObservableProperty]
    private bool _isEnglish = false;

    // ══════════════════════════════════════════
    //  OPC 连接配置
    // ══════════════════════════════════════════

    [ObservableProperty]
    private string _opcIpAddress = "127.0.0.1";

    [ObservableProperty]
    private int _opcPort = 4840;

    [ObservableProperty]
    private bool _opcAutoConnect;

    public SettingPageViewModel()
    {
        var lang = LocalizationService.Instance.CurrentLanguage;
        _isChinese = lang == "zh-CN";
        _isEnglish = lang != "zh-CN";
    }

    public SettingPageViewModel(
        IOpcUaService opcUaService,
        ILogger<SettingPageViewModel> logger) : this()
    {
        _opcUaService = opcUaService;
        _logger = logger;
    }

    /// <summary>
    /// 加载连接配置（页面激活时调用）
    /// </summary>
    public void LoadConfig()
    {
        try
        {
            var config = _opcUaService.GetCurrentConfig();
            OpcIpAddress = config.IpAddress;
            OpcPort = config.Port;
            OpcAutoConnect = config.AutoConnect;

            _logger.LogInformation("OPC 连接配置已加载");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OPC 连接配置失败");
        }
    }

    /// <summary>
    /// 保存连接配置到 appsettings.json
    /// </summary>
    [RelayCommand]
    private void SaveOpcConfig()
    {
        try
        {
            var config = _opcUaService.GetCurrentConfig();
            config.IpAddress = OpcIpAddress;
            config.Port = OpcPort;
            config.AutoConnect = OpcAutoConnect;

            // 保存到 appsettings.json
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                    ?? new Dictionary<string, object>();

                settings["OpcUa"] = new Dictionary<string, object>
                {
                    ["IpAddress"] = config.IpAddress,
                    ["Port"] = config.Port,
                    ["ReconnectIntervalMs"] = config.ReconnectIntervalMs,
                    ["MaxReconnectIntervalMs"] = config.MaxReconnectIntervalMs,
                    ["SessionTimeoutMs"] = config.SessionTimeoutMs,
                    ["KeepAliveIntervalMs"] = config.KeepAliveIntervalMs,
                    ["SecurityPolicy"] = config.SecurityPolicy,
                    ["AutoConnect"] = config.AutoConnect
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, options));
            }

            _opcUaService.ReloadConfig();
            _logger.LogInformation("OPC 连接配置已保存: {Endpoint}", config.GetEndpointUrl());

            MessageBox.Show("OPC 连接配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 OPC 配置失败");
            MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 连接 OPC 服务器
    /// </summary>
    [RelayCommand]
    private async Task ConnectOpcAsync()
    {
        try
        {
            await _opcUaService.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC 手动连接失败");
            MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 断开 OPC 连接
    /// </summary>
    [RelayCommand]
    private async Task DisconnectOpcAsync()
    {
        try
        {
            await _opcUaService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC 手动断开失败");
        }
    }

    // ══════════════════════════════════════════
    //  语言切换
    // ══════════════════════════════════════════

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

    // ══════════════════════════════════════════
    //  释放
    // ══════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
