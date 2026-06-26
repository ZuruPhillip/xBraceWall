using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CncWallStation.Localization;
using CncWallStation.Services.OpcUa;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CncWallStation.ViewModels;

public partial class SettingPageViewModel : ObservableObject
{
    private readonly IOpcUaService _opcUaService = null!;
    private readonly ILogger<SettingPageViewModel> _logger = null!;
    private static readonly string NodesFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CncWallStation", "opc_nodes.json");

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

    // ══════════════════════════════════════════
    //  OPC 节点列表管理
    // ══════════════════════════════════════════

    public ObservableCollection<OpcNodeConfig> OpcNodes { get; } = new();

    [ObservableProperty]
    private OpcNodeConfig? _selectedOpcNode;

    [ObservableProperty]
    private string _newNodeId = string.Empty;

    [ObservableProperty]
    private string _newNodeDescription = string.Empty;

    [ObservableProperty]
    private bool _newNodeIsWritable;

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
    /// 加载配置和节点列表（页面激活时调用）
    /// </summary>
    public void LoadConfig()
    {
        try
        {
            var config = _opcUaService.GetCurrentConfig();
            OpcIpAddress = config.IpAddress;
            OpcPort = config.Port;
            OpcAutoConnect = config.AutoConnect;

            LoadNodesFromFile();

            _logger.LogInformation("OPC 设置页配置已加载");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OPC 配置失败");
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
    /// 添加新节点
    /// </summary>
    [RelayCommand]
    private void AddOpcNode()
    {
        if (string.IsNullOrWhiteSpace(NewNodeId))
        {
            MessageBox.Show("请输入 NodeId", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (OpcNodes.Any(n => n.NodeId == NewNodeId.Trim()))
        {
            MessageBox.Show("该 NodeId 已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var node = new OpcNodeConfig
        {
            NodeId = NewNodeId.Trim(),
            Description = NewNodeDescription.Trim(),
            IsWritable = NewNodeIsWritable
        };

        OpcNodes.Add(node);
        SaveNodesToFile();

        // 清空输入
        NewNodeId = string.Empty;
        NewNodeDescription = string.Empty;
        NewNodeIsWritable = false;

        _logger.LogInformation("OPC 节点已添加: {NodeId} ({Description})", node.NodeId, node.Description);
    }

    /// <summary>
    /// 删除选中节点
    /// </summary>
    [RelayCommand]
    private void DeleteOpcNode()
    {
        if (SelectedOpcNode == null) return;

        var result = MessageBox.Show(
            $"确定要删除节点 \"{SelectedOpcNode.NodeId}\" 吗？",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _logger.LogInformation("OPC 节点已删除: {NodeId}", SelectedOpcNode.NodeId);
            OpcNodes.Remove(SelectedOpcNode);
            SelectedOpcNode = null;
            SaveNodesToFile();
        }
    }

    /// <summary>
    /// 清空所有节点
    /// </summary>
    [RelayCommand]
    private void ClearAllOpcNodes()
    {
        if (OpcNodes.Count == 0) return;

        var result = MessageBox.Show(
            "确定要清空所有节点配置吗？", "确认清空",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            OpcNodes.Clear();
            SaveNodesToFile();
            _logger.LogInformation("所有 OPC 节点已清空");
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
    //  节点持久化
    // ══════════════════════════════════════════

    private void LoadNodesFromFile()
    {
        try
        {
            OpcNodes.Clear();
            if (File.Exists(NodesFilePath))
            {
                var json = File.ReadAllText(NodesFilePath);
                var nodes = JsonSerializer.Deserialize<List<OpcNodeConfig>>(json);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                        OpcNodes.Add(node);
                }
                _logger.LogDebug("已加载 {Count} 个 OPC 节点配置", OpcNodes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载 OPC 节点配置文件失败");
        }
    }

    private void SaveNodesToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(NodesFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(OpcNodes.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(NodesFilePath, json);
            _logger.LogDebug("已保存 {Count} 个 OPC 节点配置", OpcNodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 OPC 节点配置文件失败");
        }
    }
}
