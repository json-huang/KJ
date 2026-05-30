using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using KJ.Plugin.Contracts;
using Microsoft.Extensions.Logging;

namespace KJ.Plugin.Host;

public sealed class PluginConnection : IAsyncDisposable
{
    private readonly ILogger _logger;
    private GrpcChannel? _channel;
    private PluginService.PluginServiceClient? _client;
    private Process? _process;
    private CancellationTokenSource? _eventsCts;
    private CancellationTokenSource? _reconnectCts;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _startedByHost;
    private int _reconnectAttempt;
    private int _eventSubscriptionGeneration;
    private bool _autoReconnectEnabled = true;
    private readonly SemaphoreSlim _connectGate = new(1, 1);

    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    static PluginConnection()
    {
        // gRPC over http:// (no TLS) for local plugins
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public PluginConnection(PluginDescriptor descriptor, ILogger logger)
    {
        Descriptor = descriptor;
        _logger = logger;
    }

    public PluginDescriptor Descriptor { get; }

    public PluginConnectionState State { get; private set; }

    public PluginManifest? Manifest { get; private set; }

    public string? LastMessage { get; private set; }

    public event EventHandler<PluginEvent>? PluginEventReceived;

    public void SetAutoReconnectEnabled(bool enabled) => _autoReconnectEnabled = enabled;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetimeCts.IsCancellationRequested)
            return false;

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is PluginConnectionState.Faulted or PluginConnectionState.Disconnected or PluginConnectionState.Reconnecting)
                ResetGrpcClient();

            if (State != PluginConnectionState.Connected)
                State = State == PluginConnectionState.Reconnecting
                    ? PluginConnectionState.Reconnecting
                    : PluginConnectionState.Connecting;

            await EnsureProcessStartedAsync(cancellationToken).ConfigureAwait(false);

            _channel ??= GrpcChannel.ForAddress(
                Descriptor.GrpcEndpoint,
                new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                    },
                });
            _client ??= new PluginService.PluginServiceClient(_channel);

            var handshake = await _client.HandshakeAsync(new HandshakeRequest
            {
                HostId = PluginProtocol.HostId,
                HostVersion = "1.0",
                ProtocolVersion = PluginProtocol.CurrentVersion,
                Capabilities =
                {
                    PluginProtocol.Capabilities.WindowHandle,
                    PluginProtocol.Capabilities.Commands,
                    PluginProtocol.Capabilities.HostEvents,
                },
            }, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);

            if (!handshake.Accepted ||
                handshake.ProtocolVersion < PluginProtocol.MinSupportedVersion ||
                handshake.ProtocolVersion > PluginProtocol.CurrentVersion)
            {
                State = PluginConnectionState.Faulted;
                LastMessage = handshake.Message ?? "Handshake rejected.";
                _logger.LogWarning("Plugin {PluginId} rejected handshake: {Message}", Descriptor.PluginId, handshake.Message);
                ScheduleReconnect("handshake rejected");
                return false;
            }

            Manifest = await _client.GetManifestAsync(new GetManifestRequest(), cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);
            State = PluginConnectionState.Connected;
            LastMessage = $"Connected: {Manifest.DisplayName}";
            Interlocked.Exchange(ref _reconnectAttempt, 0);
            StartEventSubscription();
            return true;
        }
        catch (Exception ex)
        {
            State = PluginConnectionState.Faulted;
            LastMessage = ex.Message;
            _logger.LogWarning(ex, "Failed to connect plugin {PluginId}", Descriptor.PluginId);
            ScheduleReconnect("connect failed");
            return false;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async Task<PluginWindowInfo?> GetWindowAsync(string? pageId = null, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false) || _client is null || Manifest is null)
            return null;

        var selectedPageId = pageId ?? Manifest.Pages.FirstOrDefault()?.PageId ?? "main";
        WindowHandleReply? reply = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            reply = await _client.GetWindowAsync(new GetWindowRequest { PageId = selectedPageId }, cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);
            if (reply.Available && reply.Hwnd != 0)
                break;

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        if (reply is not null && reply.Available && reply.Hwnd != 0)
            return new PluginWindowInfo(Descriptor, Manifest, selectedPageId, new IntPtr(reply.Hwnd), reply.Title);

        var fallbackHwnd = await GetProcessMainWindowHandleAsync(cancellationToken).ConfigureAwait(false);
        if (fallbackHwnd != IntPtr.Zero)
        {
            LastMessage = "Plugin returned no HWND, using process MainWindowHandle fallback.";
            return new PluginWindowInfo(Descriptor, Manifest, selectedPageId, fallbackHwnd, Manifest.DisplayName);
        }

        if (reply is null || !reply.Available || reply.Hwnd == 0)
        {
            LastMessage = $"Plugin returned no window: {reply?.Message ?? "no reply"}";
            _logger.LogWarning("Plugin {PluginId} returned no window: {Message}", Descriptor.PluginId, reply?.Message);
            return null;
        }

        return null;
    }

    public async Task<CommandReply?> InvokeCommandAsync(string commandId, IDictionary<string, string>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false) || _client is null)
            return null;

        var request = new CommandRequest { CommandId = commandId };
        if (parameters is not null)
            request.Parameters.Add(parameters);

        return await _client.InvokeCommandAsync(request, cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);
    }

    public async Task<bool> PushHostEventAsync(HostEvent hostEvent, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false) || _client is null)
            return false;

        var ack = await _client.PushHostEventAsync(hostEvent, cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);
        return ack.Success;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync().ConfigureAwait(false);
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        ResetGrpcClient();
        await TryStopOwnedProcessAsync().ConfigureAwait(false);
        _lifetimeCts.Dispose();
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (State == PluginConnectionState.Connected)
            return true;

        return await ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task EnsureProcessStartedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Descriptor.ExecutablePath))
            return Task.CompletedTask;

        if (_process is { HasExited: false })
            return Task.CompletedTask;

        var executablePath = ResolvePath(Descriptor.ExecutablePath);
        _process = FindExistingProcess(executablePath);
        if (_process is { HasExited: false })
        {
            _startedByHost = false;
            return Task.CompletedTask;
        }

        if (!File.Exists(executablePath))
        {
            _logger.LogWarning("Plugin executable not found: {Path}", executablePath);
            return Task.CompletedTask;
        }

        State = PluginConnectionState.Starting;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = ResolveWorkingDirectory(executablePath),
        };
        PluginLaunchOptions.ApplyTo(startInfo, Descriptor);
        _process = Process.Start(startInfo);
        _startedByHost = _process is not null;
        return Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);
    }

    private async Task<IntPtr> GetProcessMainWindowHandleAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (_process is { HasExited: false })
            {
                _process.Refresh();
                if (_process.MainWindowHandle != IntPtr.Zero)
                    return _process.MainWindowHandle;
            }

            if (!string.IsNullOrWhiteSpace(Descriptor.ExecutablePath))
            {
                _process = FindExistingProcess(ResolvePath(Descriptor.ExecutablePath));
                if (_process is { HasExited: false })
                {
                    _process.Refresh();
                    if (_process.MainWindowHandle != IntPtr.Zero)
                        return _process.MainWindowHandle;
                }
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        return IntPtr.Zero;
    }

    private static Process? FindExistingProcess(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!process.HasExited &&
                    string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                    return process;
            }
            catch
            {
                process.Dispose();
            }
        }

        return null;
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var descriptorDirectory = Path.GetDirectoryName(Descriptor.SourcePath);
        var baseDirectory = string.IsNullOrWhiteSpace(descriptorDirectory) ? AppContext.BaseDirectory : descriptorDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private string ResolveWorkingDirectory(string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(Descriptor.WorkingDirectory))
            return ResolvePath(Descriptor.WorkingDirectory);

        return Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
    }

    private void StartEventSubscription()
    {
        if (_client is null)
            return;

        var generation = Interlocked.Increment(ref _eventSubscriptionGeneration);
        _eventsCts?.Cancel();
        _eventsCts?.Dispose();
        _eventsCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var eventsToken = _eventsCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                using var call = _client.SubscribeEvents(new EventSubscribeRequest
                {
                    Topics = { "*" },
                }, cancellationToken: eventsToken);

                await foreach (var pluginEvent in call.ResponseStream.ReadAllAsync(eventsToken).ConfigureAwait(false))
                    PluginEventReceived?.Invoke(this, pluginEvent);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Plugin event stream ended for {PluginId}", Descriptor.PluginId);
            }
            finally
            {
                if (generation == Volatile.Read(ref _eventSubscriptionGeneration) &&
                    !_lifetimeCts.IsCancellationRequested)
                {
                    if (State == PluginConnectionState.Connected)
                    {
                        State = PluginConnectionState.Disconnected;
                        LastMessage = "Event stream ended.";
                    }

                    ScheduleReconnect("event stream ended");
                }
            }
        }, eventsToken);
    }

    private void ResetGrpcClient()
    {
        _eventsCts?.Cancel();
        _eventsCts?.Dispose();
        _eventsCts = null;
        _channel?.Dispose();
        _channel = null;
        _client = null;
        Manifest = null;
    }

    private void ScheduleReconnect(string reason)
    {
        if (!_autoReconnectEnabled || _lifetimeCts.IsCancellationRequested)
            return;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var token = _reconnectCts.Token;

        _ = Task.Run(async () =>
        {
            var attempt = Interlocked.Increment(ref _reconnectAttempt);
            var delaySeconds = Math.Min(MaxReconnectDelay.TotalSeconds, Math.Pow(2, Math.Min(attempt - 1, 5)));
            var delay = TimeSpan.FromSeconds(delaySeconds);

            try
            {
                State = PluginConnectionState.Reconnecting;
                LastMessage = $"Reconnecting in {delay.TotalSeconds:0}s ({reason})…";
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return;

                _logger.LogInformation("Reconnecting plugin {PluginId} (attempt {Attempt})", Descriptor.PluginId, attempt);
                await ConnectAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reconnect loop failed for {PluginId}", Descriptor.PluginId);
                ScheduleReconnect("reconnect loop error");
            }
        }, token);
    }

    private async Task TryStopOwnedProcessAsync()
    {
        if (!_startedByHost || _process is null)
            return;

        try
        {
            if (_process.HasExited)
                return;

            // Best-effort graceful close
            _process.CloseMainWindow();
            await Task.Delay(500).ConfigureAwait(false);

            if (_process.HasExited)
                return;

            _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to stop plugin process {PluginId}", Descriptor.PluginId);
        }
        finally
        {
            try { _process.Dispose(); } catch { }
            _process = null;
            _startedByHost = false;
        }
    }
}
