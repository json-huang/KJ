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

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            State = PluginConnectionState.Connecting;
            await EnsureProcessStartedAsync(cancellationToken).ConfigureAwait(false);

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            _channel ??= GrpcChannel.ForAddress(Descriptor.GrpcEndpoint);
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

            if (!handshake.Accepted || handshake.ProtocolVersion != PluginProtocol.CurrentVersion)
            {
                State = PluginConnectionState.Faulted;
                _logger.LogWarning("Plugin {PluginId} rejected handshake: {Message}", Descriptor.PluginId, handshake.Message);
                return false;
            }

            Manifest = await _client.GetManifestAsync(new GetManifestRequest(), cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);
            State = PluginConnectionState.Connected;
            LastMessage = $"Connected: {Manifest.DisplayName}";
            StartEventSubscription();
            return true;
        }
        catch (Exception ex)
        {
            State = PluginConnectionState.Faulted;
            LastMessage = ex.Message;
            _logger.LogWarning(ex, "Failed to connect plugin {PluginId}", Descriptor.PluginId);
            return false;
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
        _eventsCts?.Cancel();
        _eventsCts?.Dispose();
        _eventsCts = null;
        _channel?.Dispose();
        _channel = null;
        _client = null;
        await Task.CompletedTask.ConfigureAwait(false);
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
            return Task.CompletedTask;

        if (!File.Exists(executablePath))
        {
            _logger.LogWarning("Plugin executable not found: {Path}", executablePath);
            return Task.CompletedTask;
        }

        State = PluginConnectionState.Starting;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = ResolveWorkingDirectory(executablePath),
        };
        _process = Process.Start(startInfo);
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
        if (_client is null || _eventsCts is not null)
            return;

        _eventsCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                using var call = _client.SubscribeEvents(new EventSubscribeRequest
                {
                    Topics = { "*" },
                }, cancellationToken: _eventsCts.Token);

                await foreach (var pluginEvent in call.ResponseStream.ReadAllAsync(_eventsCts.Token).ConfigureAwait(false))
                    PluginEventReceived?.Invoke(this, pluginEvent);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Plugin event stream ended for {PluginId}", Descriptor.PluginId);
            }
        });
    }
}
