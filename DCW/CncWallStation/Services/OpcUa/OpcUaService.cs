using CncWallStation.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System.Collections.Concurrent;
using System.IO;

namespace CncWallStation.Services.OpcUa;

/// <summary>
/// OPC UA 通讯服务实现（单例），管理全局唯一的 OPC UA 会话。
/// 特性：自动重连（指数退避）、批量读写、节点订阅、离线隔离、批量聚合通知、异步释放。
/// 注意：本服务不直接依赖 WPF，UI 线程切换由订阅方（ViewModel）负责。
/// </summary>
public class OpcUaService : IOpcUaService, IDisposable, IAsyncDisposable
{
    private readonly ILogger<OpcUaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly object _statusLock = new();
    private readonly object _reconnectLock = new();

    /// <summary>业务读写获取会话锁的超时，避免重连期间无限阻塞。</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private Session? _session;
    private Subscription? _subscription;
    private ApplicationConfiguration? _appConfig;
    private OpcUaConfig _config;
    private OpcConnectionStatus _status = OpcConnectionStatus.Disconnected;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private volatile bool _isShuttingDown;

    // 已订阅节点缓存
    private readonly ConcurrentDictionary<string, OpcNodeConfig> _subscribedNodes = new();

    // 批量聚合通知：攒一批变化，定时统一推送，减轻 UI 压力
    private readonly ConcurrentDictionary<string, OpcNodeConfig> _pendingUpdates = new();
    private Timer? _notifyTimer;

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
                if (value == OpcConnectionStatus.Connected)
                    _logger.LogDebug("OPC 连接状态变更: {OldStatus} → {NewStatus}", old, value);
                else
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

        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_session?.Connected == true)
            {
                _logger.LogDebug("OPC UA 会话已连接，跳过重复连接");
                Status = OpcConnectionStatus.Connected;
                return;
            }

            Status = OpcConnectionStatus.Connecting;
            _logger.LogDebug("开始连接 OPC UA 服务器: {Endpoint}", _config.GetEndpointUrl());

            if (_appConfig == null)
            {
                _appConfig = CreateApplicationConfiguration();
                await _appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);
                if (_appConfig.CertificateValidator != null)
                    _appConfig.CertificateValidator.CertificateValidation += OnCertificateValidation;
            }

            var endpointDescription = CoreClientUtils.SelectEndpoint(
                _config.GetEndpointUrl(), useSecurity: _config.SecurityPolicy != "None");

            var endpointConfig = EndpointConfiguration.Create(_appConfig);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

            _session = await Session.Create(
                _appConfig,
                configuredEndpoint,
                updateBeforeConnect: false,
                "CncWallStation",
                (uint)_config.SessionTimeoutMs,
                null,
                null,
                ct
            ).ConfigureAwait(false);

            _session.KeepAliveInterval = _config.KeepAliveIntervalMs;
            _session.KeepAlive += OnKeepAlive;
            _session.SessionClosing += OnSessionClosing;

            _logger.LogDebug(
                "OPC UA 连接成功: Endpoint={Endpoint}, SessionTimeout={Timeout}ms",
                _config.GetEndpointUrl(), _config.SessionTimeoutMs);

            Status = OpcConnectionStatus.Connected;

            // 重连时恢复订阅（调用不加锁的内部方法，避免 SemaphoreSlim 重入死锁）
            if (!_subscribedNodes.IsEmpty)
                RestoreSubscriptionsInternal();
        }
        catch (OperationCanceledException)
        {
            throw;
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
        StopReconnect();
        await _sessionLock.WaitAsync().ConfigureAwait(false);
        try
        {
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
        var nodeIdsList = nodeIds.ToList();

        if (!IsConnected)
        {
            _logger.LogWarning("OPC 离线，无法读取节点: Count={Count}", nodeIdsList.Count);
            return Array.Empty<DataValue>();
        }

        try
        {
            var nodes = nodeIdsList.Select(id => new ReadValueId
            {
                NodeId = new NodeId(id),
                AttributeId = Attributes.Value
            }).ToList();

            if (!await _sessionLock.WaitAsync(LockTimeout, ct).ConfigureAwait(false))
            {
                _logger.LogWarning("读取节点获取会话锁超时（可能正在重连）: Count={Count}", nodes.Count);
                return Array.Empty<DataValue>();
            }
            try
            {
                if (_session == null || !_session.Connected)
                    return Array.Empty<DataValue>();

                _logger.LogDebug("批量读取节点: Count={Count}", nodes.Count);
                var readValueIds = new ReadValueIdCollection(nodes);
                var response = await _session.ReadAsync(
                    null, 0, TimestampsToReturn.Both, readValueIds, ct).ConfigureAwait(false);

                var results = new DataValue[response.Results.Count];
                for (int i = 0; i < response.Results.Count; i++)
                {
                    results[i] = response.Results[i];
                    if (!StatusCode.IsGood(results[i].StatusCode))
                    {
                        var nodeId = i < nodeIdsList.Count ? nodeIdsList[i] : "unknown";
                        _logger.LogError("读取节点失败: NodeId={NodeId}, StatusCode={StatusCode}",
                            nodeId, results[i].StatusCode);
                    }
                }

                for (int i = 0; i < results.Length && i < nodeIdsList.Count; i++)
                {
                    if (_subscribedNodes.TryGetValue(nodeIdsList[i], out var node))
                    {
                        node.CurrentValue = results[i].Value;
                        node.LastUpdated = DateTime.UtcNow;
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
        catch (OperationCanceledException) { return Array.Empty<DataValue>(); }
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
            var keys = nodeValues.Keys.ToList();
            var writeValues = nodeValues.Select(kv => new WriteValue
            {
                NodeId = new NodeId(kv.Key),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(kv.Value))
            }).ToList();

            if (!await _sessionLock.WaitAsync(LockTimeout, ct).ConfigureAwait(false))
            {
                _logger.LogWarning("写入节点获取会话锁超时（可能正在重连）: Count={Count}", writeValues.Count);
                return;
            }
            try
            {
                if (_session == null || !_session.Connected) return;

                _logger.LogInformation("批量写入节点: Count={Count}", writeValues.Count);
                var writeValueCollection = new WriteValueCollection(writeValues);
                var response = await _session.WriteAsync(
                    null, writeValueCollection, ct).ConfigureAwait(false);

                for (int i = 0; i < response.Results.Count; i++)
                {
                    if (!StatusCode.IsGood(response.Results[i]))
                    {
                        _logger.LogError("节点写入失败: NodeId={NodeId}, Status={Status}",
                            i < keys.Count ? keys[i] : "unknown", response.Results[i]);
                    }
                }

                _logger.LogInformation("批量写入完成: Count={Count}", writeValues.Count);
            }
            finally
            {
                _sessionLock.Release();
            }
        }
        catch (OperationCanceledException) { }
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
            SubscribeNodesInternal(nodeList);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// 订阅核心逻辑（不加锁）。调用方必须已持有 _sessionLock。
    /// </summary>
    private void SubscribeNodesInternal(List<OpcNodeConfig> nodeList)
    {
        try
        {
            if (_subscription == null)
            {
                _subscription = new Subscription(_session!.DefaultSubscription)
                {
                    PublishingInterval = _config.PublishingIntervalMs,
                    KeepAliveCount = 10,
                    LifetimeCount = 30,
                    MaxNotificationsPerPublish = 100,
                    Priority = 0,
                    PublishingEnabled = true
                };

                _session!.AddSubscription(_subscription);
                _subscription.Create();
                _logger.LogInformation("OPC 订阅已创建: PublishingInterval={Interval}ms",
                    _config.PublishingIntervalMs);
            }

            foreach (var node in nodeList)
            {
                if (_subscribedNodes.ContainsKey(node.NodeId))
                {
                    _logger.LogDebug("节点已订阅，跳过: {NodeId}", node.NodeId);
                    continue;
                }

                var monitoredItem = new MonitoredItem
                {
                    StartNodeId = new NodeId(node.NodeId),
                    AttributeId = Attributes.Value,
                    SamplingInterval = _config.SamplingIntervalMs,
                    QueueSize = 10,
                    DiscardOldest = true,
                    Handle = node  // 通过 Handle 携带节点信息，便于命名事件处理器读取
                };

                monitoredItem.Notification += OnMonitoredItemNotification;

                _subscription.AddItem(monitoredItem);
                _subscribedNodes[node.NodeId] = node;

                _logger.LogDebug("节点已加入订阅: {NodeId} ({Description})", node.NodeId, node.Description);
            }

            _subscription.ApplyChanges();
            EnsureNotifyTimer();
            _logger.LogInformation("订阅更新完成: TotalCount={Count}", _subscribedNodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建订阅失败: Count={Count}", nodeList.Count);
        }
    }

    /// <summary>
    /// 集中式订阅通知处理器（替代匿名闭包，便于解绑，防止事件泄漏）。
    /// 变化先入待推送缓存，由定时器批量聚合后触发事件。
    /// </summary>
    private void OnMonitoredItemNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification min) return;
        if (monitoredItem.Handle is not OpcNodeConfig node) return;

        try
        {
            if (_subscribedNodes.TryGetValue(node.NodeId, out var cachedNode))
            {
                cachedNode.CurrentValue = min.Value.Value;
                cachedNode.LastUpdated = DateTime.UtcNow;
                cachedNode.SourceTimestamp = min.Value.SourceTimestamp;
                cachedNode.Quality = StatusCode.IsGood(min.Value.StatusCode) ? "Good" : "Bad";

                if (!StatusCode.IsGood(min.Value.StatusCode))
                {
                    _logger.LogError("订阅节点状态异常: NodeId={NodeId}, StatusCode={StatusCode}",
                        node.NodeId, min.Value.StatusCode);
                }

                // 入待推送缓存（定时器聚合推送）
                _pendingUpdates[cachedNode.NodeId] = cachedNode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理订阅通知异常: NodeId={NodeId}", node.NodeId);
        }
    }

    /// <summary>
    /// 启动批量聚合通知定时器（幂等）。
    /// </summary>
    private void EnsureNotifyTimer()
    {
        if (_notifyTimer != null) return;
        var interval = _config.NotifyThrottleMs > 0 ? _config.NotifyThrottleMs : 200;
        _notifyTimer = new Timer(_ =>
        {
            try
            {
                if (_pendingUpdates.IsEmpty) return;
                var batch = _pendingUpdates.Values.ToList();
                _pendingUpdates.Clear();
                NodeValuesUpdated?.Invoke(this, batch);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批量推送订阅通知异常");
            }
        }, null, dueTime: interval, period: interval);
    }

    /// <inheritdoc />
    public void UnsubscribeAll()
    {
        try
        {
            _sessionLock.Wait();
            try
            {
                DisposeSubscriptionInternal();
                _subscribedNodes.Clear();
                _pendingUpdates.Clear();
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

            if (!await _sessionLock.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                return false;
            try
            {
                if (_session == null || !_session.Connected)
                    return false;

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
    /// 开始自动重连（指数退避）。加锁保护 _reconnectCts，且幂等防止多重重连。
    /// </summary>
    private void StartReconnect()
    {
        CancellationToken ct;
        lock (_reconnectLock)
        {
            if (_isShuttingDown) return;

            if (_reconnectCts != null && !_reconnectCts.IsCancellationRequested)
            {
                _logger.LogDebug("重连任务已在进行，跳过重复启动");
                return;
            }

            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();
            ct = _reconnectCts.Token;
        }

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
                        return;
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

                delay = Math.Min(delay * 2, _config.MaxReconnectIntervalMs);
            }
        }, ct);
    }

    /// <summary>停止重连（加锁）。</summary>
    private void StopReconnect()
    {
        lock (_reconnectLock)
        {
            try { _reconnectCts?.Cancel(); } catch (ObjectDisposedException) { }
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }
    }

    /// <summary>
    /// 恢复订阅（不加锁版本，供 ConnectAsync 已持锁时调用）。
    /// </summary>
    private void RestoreSubscriptionsInternal()
    {
        var nodes = _subscribedNodes.Values.ToList();
        _subscribedNodes.Clear();
        _pendingUpdates.Clear();
        DisposeSubscriptionInternal();

        if (nodes.Count > 0)
        {
            SubscribeNodesInternal(nodes);
            _logger.LogInformation("重连后恢复订阅: Count={Count}", nodes.Count);
        }
    }

    /// <summary>
    /// 释放 Subscription 并解绑所有 MonitoredItem 事件（防泄漏）。调用方需已持锁。
    /// </summary>
    private void DisposeSubscriptionInternal()
    {
        if (_subscription == null) return;
        try
        {
            foreach (var item in _subscription.MonitoredItems)
                item.Notification -= OnMonitoredItemNotification;
            try { _subscription.Delete(true); } catch { /* 会话已失效时忽略 */ }
            _subscription.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放订阅资源时异常");
        }
        finally
        {
            _subscription = null;
        }
    }

    /// <summary>清理会话资源。调用方需已持锁。</summary>
    private async Task CleanupSessionAsync()
    {
        DisposeSubscriptionInternal();

        try
        {
            if (_session != null)
            {
                _session.KeepAlive -= OnKeepAlive;
                _session.SessionClosing -= OnSessionClosing;

                if (_session.Connected)
                    await _session.CloseAsync().ConfigureAwait(false);

                _session.Dispose();
                _session = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关闭 OPC 会话时异常");
        }
    }

    /// <summary>KeepAlive 回调——检测断线并触发重连。</summary>
    private void OnKeepAlive(ISession sender, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            _logger.LogWarning("OPC KeepAlive 异常: {Status}", e.Status);
            _ = Task.Run(async () =>
            {
                await _sessionLock.WaitAsync().ConfigureAwait(false);
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

    private void OnSessionClosing(object? sender, EventArgs e)
        => _logger.LogWarning("OPC UA 会话即将关闭");

    /// <summary>
    /// 证书验证回调。是否接受未信任证书由配置决定，默认生产环境严格校验。
    /// </summary>
    private void OnCertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        if (_config.AcceptUntrustedCertificates)
        {
            e.Accept = true;
            _logger.LogWarning("OPC 证书验证: Accept（已配置接受未信任证书，请勿用于生产）");
        }
        else
        {
            _logger.LogError("OPC 证书验证失败（未信任）: {Subject}", e.Certificate?.Subject);
        }
    }

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
                RejectSHA1SignedCertificates = true,
                MinimumCertificateKeySize = 2048,
                AutoAcceptUntrustedCertificates = _config.AcceptUntrustedCertificates
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
                TraceMasks = 0
            }
        };

        Directory.CreateDirectory(config.SecurityConfiguration.ApplicationCertificate.StorePath);
        Directory.CreateDirectory(config.SecurityConfiguration.TrustedPeerCertificates.StorePath);

        return config;
    }

    private OpcUaConfig BindConfig()
    {
        var config = new OpcUaConfig();
        _configuration.GetSection("OpcUa").Bind(config);
        return config;
    }

    // ══════════════════════════════════════════
    //  释放
    // ══════════════════════════════════════════

    /// <summary>
    /// 异步释放（WPF/Host 首选路径，不阻塞 UI 线程）。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _isShuttingDown = true;

        StopReconnect();

        _notifyTimer?.Dispose();
        _notifyTimer = null;

        try
        {
            if (await _sessionLock.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false))
            {
                try { await CleanupSessionAsync().ConfigureAwait(false); }
                finally { _sessionLock.Release(); }
            }
        }
        catch { /* 静默，避免影响进程退出 */ }
        finally
        {
            _sessionLock.Dispose();
        }

        if (_appConfig?.CertificateValidator != null)
            _appConfig.CertificateValidator.CertificateValidation -= OnCertificateValidation;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 同步释放兜底。用 Task.Run 把清理挪到线程池线程，避免 WPF UI 线程死锁。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isShuttingDown = true;

        StopReconnect();

        _notifyTimer?.Dispose();
        _notifyTimer = null;

        try
        {
            Task.Run(async () =>
            {
                if (await _sessionLock.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false))
                {
                    try { await CleanupSessionAsync().ConfigureAwait(false); }
                    finally { _sessionLock.Release(); }
                }
            }).GetAwaiter().GetResult();
        }
        catch { /* 静默 */ }
        finally
        {
            _sessionLock.Dispose();
        }

        if (_appConfig?.CertificateValidator != null)
            _appConfig.CertificateValidator.CertificateValidation -= OnCertificateValidation;

        GC.SuppressFinalize(this);
    }
}