using CncWallStation.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System.Collections.Concurrent;
using System.IO;

namespace CncWallStation.Services.OpcUa;

/// <summary>
/// OPC UA 通讯服务实现（单例），管理全局唯一的 OPC UA 会话
/// 特性：自动重连（指数退避）、批量读写、节点订阅、离线隔离、异常日志
/// </summary>
public class OpcUaService : IOpcUaService, IDisposable
{
    private readonly ILogger<OpcUaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly object _statusLock = new();

    private Session? _session;
    private Subscription? _subscription;
    private ApplicationConfiguration? _appConfig;
    private OpcUaConfig _config;
    private OpcConnectionStatus _status = OpcConnectionStatus.Disconnected;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private bool _isShuttingDown;

    // 已订阅节点缓存，用于值更新通知
    private readonly ConcurrentDictionary<string, OpcNodeConfig> _subscribedNodes = new();

    /// <inheritdoc />
    public OpcConnectionStatus Status
    {
        get { lock (_statusLock) return _status; }
        private set
        {
            OpcConnectionStatus old;
            lock (_statusLock)
            {
                old = _status;
                _status = value;
            }
            if (old != value)
            {
                _logger.LogInformation("OPC 连接状态变更: {OldStatus} → {NewStatus}", old, value);
                StatusChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc />
    public bool IsConnected => Status == OpcConnectionStatus.Connected && _session?.Connected == true;

    /// <inheritdoc />
    public event EventHandler<OpcConnectionStatus>? StatusChanged;

    /// <inheritdoc />
    public event EventHandler<IReadOnlyList<OpcNodeConfig>>? NodeValuesUpdated;

    public OpcUaService(ILogger<OpcUaService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _config = BindConfig();
    }

    /// <inheritdoc />
    public void ReloadConfig()
    {
        _config = BindConfig();
        _logger.LogInformation("OPC 配置已重载: Endpoint={Endpoint}", _config.GetEndpointUrl());
    }

    /// <inheritdoc />
    public OpcUaConfig GetCurrentConfig() => _config;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_isShuttingDown)
        {
            _logger.LogWarning("服务正在关闭，拒绝连接请求");
            return;
        }

        await _sessionLock.WaitAsync(ct);
        try
        {
            if (_session?.Connected == true)
            {
                _logger.LogInformation("OPC UA 会话已连接，跳过重复连接");
                Status = OpcConnectionStatus.Connected;
                return;
            }

            Status = OpcConnectionStatus.Connecting;
            _logger.LogInformation("开始连接 OPC UA 服务器: {Endpoint}", _config.GetEndpointUrl());

            // 初始化 ApplicationConfiguration（仅首次需要）
            if (_appConfig == null)
            {
                _appConfig = CreateApplicationConfiguration();
                await _appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);
                if (_appConfig.CertificateValidator != null)
                    _appConfig.CertificateValidator.CertificateValidation += OnCertificateValidation;
            }

            // 创建会话
            var endpointDescription = CoreClientUtils.SelectEndpoint(
                _config.GetEndpointUrl(), useSecurity: _config.SecurityPolicy != "None");

            var endpointConfig = EndpointConfiguration.Create(_appConfig);
            var configureEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

            _session = await Session.Create(
                _appConfig,
                configureEndpoint,
                false,
                "CncWallStation",
                (uint)_config.SessionTimeoutMs,
                null,
                null,
                ct
            ).ConfigureAwait(false);

            // 注册会话事件
            _session.KeepAliveInterval = _config.KeepAliveIntervalMs;
            _session.KeepAlive += OnKeepAlive;
            _session.SessionClosing += OnSessionClosing;

            _logger.LogInformation(
                "OPC UA 连接成功: Endpoint={Endpoint}, SessionTimeout={Timeout}ms",
                _config.GetEndpointUrl(), _config.SessionTimeoutMs);

            Status = OpcConnectionStatus.Connected;

            // 重连时恢复已有订阅
            if (!_subscribedNodes.IsEmpty)
            {
                await RestoreSubscriptionsAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 连接失败: {Endpoint}", _config.GetEndpointUrl());
            Status = OpcConnectionStatus.Error;
            StartReconnect();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            StopReconnect();
            await CleanupSessionAsync().ConfigureAwait(false);
            Status = OpcConnectionStatus.Disconnected;
            _logger.LogInformation("OPC UA 已手动断开");
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataValue>> ReadNodesAsync(IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("OPC 离线，无法读取节点: Count={Count}", nodeIds.Count());
            return Array.Empty<DataValue>();
        }

        var nodeIdsList = nodeIds.ToList();
        try
        {
            var nodes = nodeIdsList.Select(id => new ReadValueId
            {
                NodeId = new NodeId(id),
                AttributeId = Attributes.Value
            }).ToList();

            await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_session == null)
                    return Array.Empty<DataValue>();

                _logger.LogDebug("批量读取节点: Count={Count}", nodes.Count);
                var readValueIds = new ReadValueIdCollection(nodes);
                var response = await _session.ReadAsync(
                    null, 0, TimestampsToReturn.Both, readValueIds, ct).ConfigureAwait(false);

                var results = new DataValue[response.Results.Count];
                for (int i = 0; i < response.Results.Count; i++)
                {
                    results[i] = response.Results[i];
                }

                // 更新订阅缓存中的值
                for (int i = 0; i < results.Length && i < nodeIdsList.Count; i++)
                {
                    if (_subscribedNodes.TryGetValue(nodeIdsList[i], out var node))
                    {
                        node.CurrentValue = results[i].Value;
                        node.LastUpdated = DateTime.Now;
                        node.SourceTimestamp = results[i].SourceTimestamp;
                        node.Quality = StatusCode.IsGood(results[i].StatusCode) ? "Good" : "Bad";
                    }
                }

                _logger.LogDebug("批量读取完成: Requested={Requested}, Returned={Returned}",
                    nodes.Count, results.Length);

                return results;
            }
            finally
            {
                _sessionLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量读取节点失败: Count={Count}", nodeIdsList.Count);
            return Array.Empty<DataValue>();
        }
    }

    /// <inheritdoc />
    public async Task WriteNodesAsync(IReadOnlyDictionary<string, object> nodeValues, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("OPC 离线，无法写入节点: Count={Count}", nodeValues.Count);
            return;
        }

        try
        {
            var writeValues = nodeValues.Select(kv => new WriteValue
            {
                NodeId = new NodeId(kv.Key),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(kv.Value))
            }).ToList();

            await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_session == null) return;

                _logger.LogInformation("批量写入节点: Count={Count}", writeValues.Count);
                var writeValueCollection = new WriteValueCollection(writeValues);
                var response = await _session.WriteAsync(
                    null, writeValueCollection, ct).ConfigureAwait(false);

                // 检查写入结果
                for (int i = 0; i < response.Results.Count; i++)
                {
                    if (!StatusCode.IsGood(response.Results[i]))
                    {
                        _logger.LogWarning("节点写入失败: NodeId={NodeId}, Status={Status}",
                            nodeValues.Keys.ElementAt(i), response.Results[i]);
                    }
                }

                _logger.LogInformation("批量写入完成: Count={Count}", writeValues.Count);
            }
            finally
            {
                _sessionLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量写入节点失败: Count={Count}", nodeValues.Count);
        }
    }

    /// <inheritdoc />
    public async Task SubscribeNodesAsync(IEnumerable<OpcNodeConfig> nodes, CancellationToken ct = default)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return;

        if (!IsConnected)
        {
            _logger.LogWarning("OPC 离线，暂存订阅节点（连接后将恢复）: Count={Count}", nodeList.Count);
            foreach (var node in nodeList)
                _subscribedNodes[node.NodeId] = node;
            return;
        }

        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 创建或获取 Subscription
            if (_subscription == null)
            {
                _subscription = new Subscription
                {
                    PublishingInterval = 1000,
                    KeepAliveCount = 10,
                    LifetimeCount = 30,
                    MaxNotificationsPerPublish = 100,
                    Priority = 0,
                    PublishingEnabled = true
                };

                _session!.AddSubscription(_subscription);
                _subscription.Create();
                _logger.LogInformation("OPC 订阅已创建: PublishingInterval=1000ms");
            }

            // 添加 MonitoredItems
            foreach (var node in nodeList)
            {
                // 避免重复订阅
                if (_subscribedNodes.ContainsKey(node.NodeId))
                {
                    _logger.LogDebug("节点已订阅，跳过: {NodeId}", node.NodeId);
                    continue;
                }

                var monitoredItem = new MonitoredItem
                {
                    StartNodeId = new NodeId(node.NodeId),
                    AttributeId = Attributes.Value,
                    SamplingInterval = 500,
                    QueueSize = 10,
                    DiscardOldest = true
                };

                monitoredItem.Notification += (mi, e) =>
                {
                    var notification = e as MonitoredItemNotificationEventArgs;
                    if (notification?.NotificationValue is not MonitoredItemNotification min) return;

                    try
                    {
                        if (_subscribedNodes.TryGetValue(node.NodeId, out var cachedNode))
                        {
                            cachedNode.CurrentValue = min.Value.Value;
                            cachedNode.LastUpdated = DateTime.Now;
                            cachedNode.SourceTimestamp = min.Value.SourceTimestamp;
                            cachedNode.Quality = StatusCode.IsGood(min.Value.StatusCode) ? "Good" : "Bad";

                            // 触发值更新事件
                            NodeValuesUpdated?.Invoke(this, new[] { cachedNode });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "处理订阅通知异常: NodeId={NodeId}", node.NodeId);
                    }
                };

                _subscription.AddItem(monitoredItem);
                _subscribedNodes[node.NodeId] = node;

                _logger.LogDebug("节点已加入订阅: {NodeId} ({Description})", node.NodeId, node.Description);
            }

            _subscription.ApplyChanges();
            _logger.LogInformation("订阅更新完成: TotalCount={Count}", _subscribedNodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建订阅失败: Count={Count}", nodeList.Count);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <inheritdoc />
    public void UnsubscribeAll()
    {
        try
        {
            _sessionLock.Wait();
            try
            {
                _subscribedNodes.Clear();
                _subscription?.Delete(true);
                _subscription?.Dispose();
                _subscription = null;
                _logger.LogInformation("所有 OPC 订阅已取消");
            }
            finally
            {
                _sessionLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消订阅失败");
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OpcNodeConfig>> GetSubscribedNodesAsync()
    {
        var nodes = _subscribedNodes.Values
            .OrderBy(n => n.Description)
            .ThenBy(n => n.NodeId)
            .ToList() as IReadOnlyList<OpcNodeConfig>;
        return Task.FromResult(nodes);
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            if (_session == null || !_session.Connected)
                return false;

            await _sessionLock.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            try
            {
                // 读取 ServerStatus 节点验证连接
                var nodes = new ReadValueIdCollection
                {
                    new ReadValueId
                    {
                        NodeId = Variables.Server_ServerStatus,
                        AttributeId = Attributes.Value
                    }
                };

                var response = await _session.ReadAsync(
                    null, 0, TimestampsToReturn.Neither, nodes, CancellationToken.None).ConfigureAwait(false);

                return response.Results.Count > 0 && StatusCode.IsGood(response.Results[0].StatusCode);
            }
            finally
            {
                _sessionLock.Release();
            }
        }
        catch
        {
            return false;
        }
    }

    // ══════════════════════════════════════════
    //  内部方法
    // ══════════════════════════════════════════

    /// <summary>
    /// 开始自动重连（指数退避策略）
    /// </summary>
    private void StartReconnect()
    {
        StopReconnect();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;

        _ = Task.Run(async () =>
        {
            int attempt = 0;
            int delay = _config.ReconnectIntervalMs;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    _logger.LogWarning("OPC 重连尝试 #{Attempt}, 等待 {Delay}ms", attempt, delay);
                    await Task.Delay(delay, ct).ConfigureAwait(false);

                    await ConnectAsync(ct).ConfigureAwait(false);

                    if (IsConnected)
                    {
                        _logger.LogInformation("OPC 重连成功 (第 {Attempt} 次尝试)", attempt);
                        return; // 重连成功，退出重连循环
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OPC 重连尝试 #{Attempt} 失败", attempt);
                }

                // 指数退避：每次翻倍，上限为 MaxReconnectIntervalMs
                delay = Math.Min(delay * 2, _config.MaxReconnectIntervalMs);
            }
        }, ct);
    }

    /// <summary>
    /// 停止重连
    /// </summary>
    private void StopReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    /// <summary>
    /// 恢复之前的订阅（重连后调用）
    /// </summary>
    private async Task RestoreSubscriptionsAsync(CancellationToken ct)
    {
        var nodes = _subscribedNodes.Values.ToList();
        _subscribedNodes.Clear(); // 清空旧缓存，由 SubscribeNodesAsync 重新填充
        _subscription?.Delete(true);
        _subscription?.Dispose();
        _subscription = null;

        if (nodes.Count > 0)
        {
            await SubscribeNodesAsync(nodes, ct).ConfigureAwait(false);
            _logger.LogInformation("重连后恢复订阅: Count={Count}", nodes.Count);
        }
    }

    /// <summary>
    /// 清理会话资源
    /// </summary>
    private async Task CleanupSessionAsync()
    {
        try
        {
            if (_subscription != null)
            {
                _subscription.Delete(true);
                _subscription.Dispose();
                _subscription = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理订阅资源时异常");
        }

        try
        {
            if (_session != null)
            {
                _session.KeepAlive -= OnKeepAlive;
                _session.SessionClosing -= OnSessionClosing;

                if (_session.Connected)
                {
                    await _session.CloseAsync().ConfigureAwait(false);
                }
                _session.Dispose();
                _session = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关闭 OPC 会话时异常");
        }
    }

    /// <summary>
    /// KeepAlive 回调——检测断线并触发重连
    /// </summary>
    private void OnKeepAlive(ISession sender, KeepAliveEventArgs e)
    {
        if (e.Status == null || ServiceResult.IsBad(e.Status))
        {
            _logger.LogWarning("OPC KeepAlive 异常: {Status}", e.Status);
            _ = Task.Run(async () =>
            {
                await _sessionLock.WaitAsync();
                try
                {
                    await CleanupSessionAsync().ConfigureAwait(false);
                }
                finally
                {
                    _sessionLock.Release();
                }

                if (!_isShuttingDown)
                {
                    Status = OpcConnectionStatus.Disconnected;
                    StartReconnect();
                }
            });
        }
    }

    /// <summary>
    /// 会话关闭回调
    /// </summary>
    private void OnSessionClosing(object? sender, EventArgs e)
    {
        _logger.LogWarning("OPC UA 会话即将关闭");
    }

    /// <summary>
    /// 证书验证回调（默认接受所有证书，生产环境需严格验证）
    /// </summary>
    private void OnCertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        // 开发/测试环境接受所有证书，生产环境应替换为严格验证逻辑
        e.Accept = true;
        _logger.LogDebug("OPC 证书验证: Accept (开发模式)");
    }

    /// <summary>
    /// 创建 ApplicationConfiguration
    /// </summary>
    private ApplicationConfiguration CreateApplicationConfiguration()
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "CncWallStation",
            ApplicationUri = "urn:CncWallStation:opcua-client",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CncWallStation", "OPC", "certs"),
                    SubjectName = "CN=CncWallStation, O=ZURU"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CncWallStation", "OPC", "trusted")
                },
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
                AutoAcceptUntrustedCertificates = true
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 30000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = _config.SessionTimeoutMs
            },
            TraceConfiguration = new TraceConfiguration
            {
                DeleteOnLoad = true,
                TraceMasks = 0 // 不启用 SDK 内部 Trace
            }
        };

        // 确保证书目录存在
        Directory.CreateDirectory(config.SecurityConfiguration.ApplicationCertificate.StorePath);
        Directory.CreateDirectory(config.SecurityConfiguration.TrustedPeerCertificates.StorePath);

        return config;
    }

    /// <summary>
    /// 从 IConfiguration 绑定 OpcUaConfig
    /// </summary>
    private OpcUaConfig BindConfig()
    {
        var config = new OpcUaConfig();
        var section = _configuration.GetSection("OpcUa");
        section.Bind(config);
        return config;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isShuttingDown = true;

        StopReconnect();

        try
        {
            _sessionLock.Wait(TimeSpan.FromSeconds(3));
            CleanupSessionAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // 静默，避免影响进程退出
        }
        finally
        {
            _sessionLock.Dispose();
        }

        _reconnectCts?.Dispose();
        if (_appConfig?.CertificateValidator != null)
            _appConfig.CertificateValidator.CertificateValidation -= OnCertificateValidation;

        GC.SuppressFinalize(this);
    }
}
