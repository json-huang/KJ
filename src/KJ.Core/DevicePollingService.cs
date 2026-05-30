
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using Microsoft.Extensions.Logging;

namespace KJ.Core;

/// <summary>
/// 真实设备采集服务。支持：
/// - 按设备并行轮询
/// - 断线自动重连（指数退避）
/// - 每个标签独立轮询间隔
/// - 告警评估联动
/// </summary>
public sealed class DevicePollingService : ICommsService, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly IDeviceDriverFactory _driverFactory;
    private readonly ITagConfigStore _tagConfigStore;
    private readonly ITagStore _tagStore;
    private readonly IAlarmService? _alarmService;
    private readonly DiagnosticHub _diagnostics;
    private readonly ILogger<DevicePollingService>? _logger;

    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private readonly Dictionary<string, DeviceSession> _sessions = new();
    private readonly object _gate = new();

    // 重连配置
    private const int MaxReconnectDelayMs = 30_000;
    private const int InitialReconnectDelayMs = 1_000;

    public DevicePollingService(
        IDeviceManager deviceManager,
        IDeviceDriverFactory driverFactory,
        ITagConfigStore tagConfigStore,
        ITagStore tagStore,
        DiagnosticHub diagnostics,
        IAlarmService? alarmService = null,
        ILogger<DevicePollingService>? logger = null)
    {
        _deviceManager = deviceManager;
        _driverFactory = driverFactory;
        _tagConfigStore = tagConfigStore;
        _tagStore = tagStore;
        _alarmService = alarmService;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is not null)
            return;

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _loopCts.Token;

        await ConnectAllDevicesAsync(ct).ConfigureAwait(false);

        _loop = Task.Run(() => PollingLoopAsync(ct), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is null)
            return;

        try
        {
            _loopCts?.Cancel();
            await Task.WhenAny(_loop, Task.Delay(3000, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            _loop = null;
            _loopCts?.Dispose();
            _loopCts = null;
            await DisconnectAllDevicesAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    // ── 连接管理 ────────────────────────────────────────────────────────

    private async Task ConnectAllDevicesAsync(CancellationToken ct)
    {
        var devices = _deviceManager.ListDevices();
        foreach (var device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.Host))
            {
                _logger?.LogWarning("Device {DeviceId} has no host configured, skipping", device.DeviceId);
                continue;
            }

            await ConnectDeviceAsync(device, ct).ConfigureAwait(false);
        }
    }

    private async Task ConnectDeviceAsync(DeviceDescriptor device, CancellationToken ct)
    {
        try
        {
            var driver = _driverFactory.Create(device.DriverType);
            var endpoint = new DeviceEndpoint(device.Host, device.Port, device.Extra);
            await driver.ConnectAsync(endpoint, ct).ConfigureAwait(false);

            lock (_gate)
            {
                _sessions[device.DeviceId] = new DeviceSession
                {
                    Driver = driver,
                    Device = device,
                    IsConnected = true,
                    ReconnectDelayMs = InitialReconnectDelayMs,
                    LastSuccessfulRead = DateTimeOffset.Now,
                };
            }

            _deviceManager.UpdateDeviceState(device.DeviceId, "Connected");
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.TransportOpen, nameof(DevicePollingService),
                DeviceId: device.DeviceId,
                Message: $"Connected to {device.DisplayName} ({device.Host}:{device.Port})"));
        }
        catch (Exception ex)
        {
            _deviceManager.UpdateDeviceState(device.DeviceId, "Faulted");
            _logger?.LogError(ex, "Failed to connect to device {DeviceId}", device.DeviceId);
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(DevicePollingService),
                DeviceId: device.DeviceId,
                Message: $"Connect failed for {device.DisplayName}",
                Error: ex.Message));

            // 注册重连
            lock (_gate)
            {
                _sessions[device.DeviceId] = new DeviceSession
                {
                    Driver = null,
                    Device = device,
                    IsConnected = false,
                    ReconnectDelayMs = InitialReconnectDelayMs,
                    NextReconnectAt = DateTimeOffset.Now.AddMilliseconds(InitialReconnectDelayMs),
                };
            }
        }
    }

    // ── 手动连接/断开（UI 用）──────────────────────────────────────────

    public async Task ConnectNowAsync(string deviceId, CancellationToken ct = default)
    {
        var device = _deviceManager.GetDevice(deviceId);
        if (device is null)
            throw new InvalidOperationException($"Device '{deviceId}' not found");

        if (string.IsNullOrWhiteSpace(device.Host))
            throw new InvalidOperationException($"Device '{deviceId}' has no host configured");

        // 若已有会话且已连接，则不重复连接
        lock (_gate)
        {
            if (_sessions.TryGetValue(deviceId, out var s) && s.Driver is { State: DriverConnectionState.Connected })
                return;
        }

        await ConnectDeviceAsync(device, ct).ConfigureAwait(false);
    }

    public bool TryGetConnectedDriver(string deviceId, out IDeviceDriver? driver)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(deviceId, out var session) &&
                session.Driver is { State: DriverConnectionState.Connected })
            {
                driver = session.Driver;
                return true;
            }
        }

        driver = null;
        return false;
    }

    public async Task DisconnectNowAsync(string deviceId, CancellationToken ct = default)
    {
        DeviceSession? session = null;
        lock (_gate)
        {
            if (_sessions.TryGetValue(deviceId, out var s))
            {
                session = s;
                _sessions.Remove(deviceId);
            }
        }

        if (session?.Driver is not null)
        {
            try { await session.Driver.DisconnectAsync(ct).ConfigureAwait(false); } catch { /* best-effort */ }
            try { await session.Driver.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
        }

        try { _deviceManager.UpdateDeviceState(deviceId, "Disconnected"); } catch { /* ignore */ }
    }

    private async Task DisconnectAllDevicesAsync()
    {
        List<DeviceSession> sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToList();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            try
            {
                if (session.Driver is not null)
                {
                    await session.Driver.DisconnectAsync().ConfigureAwait(false);
                    await session.Driver.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disconnecting device {DeviceId}", session.Device.DeviceId);
            }
        }
    }

    /// <summary>尝试重连断开的设备。</summary>
    private async Task TryReconnectAsync(CancellationToken ct)
    {
        List<DeviceSession> toReconnect;
        lock (_gate)
        {
            toReconnect = _sessions.Values
                .Where(s => !s.IsConnected && s.NextReconnectAt <= DateTimeOffset.Now)
                .ToList();
        }

        foreach (var session in toReconnect)
        {
            try
            {
                var driver = _driverFactory.Create(session.Device.DriverType);
                var endpoint = new DeviceEndpoint(session.Device.Host, session.Device.Port, session.Device.Extra);
                await driver.ConnectAsync(endpoint, ct).ConfigureAwait(false);

                lock (_gate)
                {
                    session.Driver = driver;
                    session.IsConnected = true;
                    session.ReconnectDelayMs = InitialReconnectDelayMs;
                    session.LastSuccessfulRead = DateTimeOffset.Now;
                    session.ConsecutiveFailures = 0;
                }

                _deviceManager.UpdateDeviceState(session.Device.DeviceId, "Connected");
                _logger?.LogInformation("Reconnected to device {DeviceId}", session.Device.DeviceId);
                _diagnostics.Publish(new DiagnosticEvent(
                    DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                    DiagnosticStage.TransportOpen, nameof(DevicePollingService),
                    DeviceId: session.Device.DeviceId,
                    Message: $"Reconnected to {session.Device.DisplayName}"));
            }
            catch (Exception ex)
            {
                // 指数退避
                lock (_gate)
                {
                    session.ReconnectDelayMs = Math.Min(session.ReconnectDelayMs * 2, MaxReconnectDelayMs);
                    session.NextReconnectAt = DateTimeOffset.Now.AddMilliseconds(session.ReconnectDelayMs);
                }

                _logger?.LogWarning(ex, "Reconnect failed for device {DeviceId}, retry in {Delay}ms",
                    session.Device.DeviceId, session.ReconnectDelayMs);
            }
        }
    }

    // ── 轮询循环 ────────────────────────────────────────────────────────

    private async Task PollingLoopAsync(CancellationToken ct)
    {
        // 按设备分组标签
        var allTags = _tagConfigStore.GetAllTags();
        var tagsByDevice = allTags.GroupBy(t => t.DeviceId).ToDictionary(g => g.Key, g => g.ToList());

        // 每个标签的下次轮询时间
        var nextPoll = new Dictionary<Guid, DateTimeOffset>();
        var now = DateTimeOffset.Now;
        foreach (var tag in allTags)
            nextPoll[tag.TagId] = now;

        while (!ct.IsCancellationRequested)
        {
            var loopStart = DateTimeOffset.Now;

            // 尝试重连断开的设备
            await TryReconnectAsync(ct).ConfigureAwait(false);

            // 按设备并行轮询
            var tasks = new List<Task>();
            lock (_gate)
            {
                foreach (var (deviceId, session) in _sessions)
                {
                    if (!session.IsConnected || session.Driver is null) continue;
                    if (!tagsByDevice.TryGetValue(deviceId, out var deviceTags)) continue;

                    tasks.Add(PollDeviceAsync(session, deviceTags, nextPoll, ct));
                }
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);

            // 最小轮询周期 100ms
            var elapsed = DateTimeOffset.Now - loopStart;
            var delay = Math.Max(100, 500 - (int)elapsed.TotalMilliseconds);
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollDeviceAsync(
        DeviceSession session,
        List<TagConfig> tags,
        Dictionary<Guid, DateTimeOffset> nextPoll,
        CancellationToken ct)
    {
        var now = DateTimeOffset.Now;

        foreach (var tag in tags)
        {
            if (ct.IsCancellationRequested) break;

            // 检查轮询间隔
            if (nextPoll.TryGetValue(tag.TagId, out var next) && now < next)
                continue;

            // 设置下次轮询时间
            var intervalMs = tag.PollIntervalMs > 0 ? tag.PollIntervalMs : 1000;
            nextPoll[tag.TagId] = now.AddMilliseconds(intervalMs);

            try
            {
                var tagAddress = new TagAddress(tag.Address, tag.ValueType);
                var request = new TagReadRequest(tag.TagKey, tagAddress);
                var result = await session.Driver!.ReadAsync(request, ct).ConfigureAwait(false);

                if (result.Success)
                {
                    _tagStore.Upsert(new TagValue(new TagId(tag.TagKey), result.Value, TagQuality.Good, result.Timestamp));
                    _alarmService?.Evaluate(tag.TagKey, result.Value);

                    lock (_gate)
                    {
                        session.LastSuccessfulRead = DateTimeOffset.Now;
                        session.ConsecutiveFailures = 0;
                    }
                }
                else
                {
                    _tagStore.Upsert(new TagValue(new TagId(tag.TagKey), null, TagQuality.Bad, result.Timestamp));

                    lock (_gate)
                    {
                        session.ConsecutiveFailures++;
                    }

                    // 连续失败 5 次，标记断开
                    if (session.ConsecutiveFailures >= 5)
                    {
                        lock (_gate)
                        {
                            session.IsConnected = false;
                            session.NextReconnectAt = DateTimeOffset.Now.AddMilliseconds(InitialReconnectDelayMs);
                            session.ReconnectDelayMs = InitialReconnectDelayMs;
                        }
                        _deviceManager.UpdateDeviceState(session.Device.DeviceId, "Disconnected");
                        _logger?.LogWarning("Device {DeviceId} marked disconnected after {Failures} consecutive failures",
                            session.Device.DeviceId, session.ConsecutiveFailures);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error polling tag {TagKey} from device {DeviceId}",
                    tag.TagKey, session.Device.DeviceId);

                lock (_gate)
                {
                    session.ConsecutiveFailures++;
                }

                if (session.ConsecutiveFailures >= 5)
                {
                    lock (_gate)
                    {
                        session.IsConnected = false;
                        session.NextReconnectAt = DateTimeOffset.Now.AddMilliseconds(InitialReconnectDelayMs);
                    }
                    _deviceManager.UpdateDeviceState(session.Device.DeviceId, "Disconnected");
                    break;
                }
            }
        }
    }

    // ── 内部类型 ────────────────────────────────────────────────────────

    private sealed class DeviceSession
    {
        public IDeviceDriver? Driver { get; set; }
        public required DeviceDescriptor Device { get; init; }
        public bool IsConnected { get; set; }
        public int ReconnectDelayMs { get; set; } = InitialReconnectDelayMs;
        public DateTimeOffset NextReconnectAt { get; set; }
        public DateTimeOffset LastSuccessfulRead { get; set; }
        public int ConsecutiveFailures { get; set; }
    }
}
