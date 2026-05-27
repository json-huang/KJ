using System.Text.Json;
using KJ.Plugin.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KJ.Plugin.Host;

public sealed class PluginManager : IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginManager> _logger;
    private readonly List<PluginConnection> _connections = new();

    public PluginManager(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<PluginManager>();
    }

    public IReadOnlyList<PluginConnection> Connections => _connections;

    public event EventHandler<PluginEvent>? PluginEventReceived;

    public async Task<IReadOnlyList<PluginConnection>> LoadAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(pluginDirectory);

        foreach (var descriptorPath in Directory.EnumerateFiles(pluginDirectory, "*.plugin.json", SearchOption.TopDirectoryOnly))
        {
            var descriptor = await ReadDescriptorAsync(descriptorPath, cancellationToken).ConfigureAwait(false);
            if (descriptor is null)
                continue;

            if (_connections.Any(x => string.Equals(x.Descriptor.PluginId, descriptor.PluginId, StringComparison.OrdinalIgnoreCase)))
                continue;

            var connection = new PluginConnection(descriptor, _loggerFactory.CreateLogger<PluginConnection>());
            connection.PluginEventReceived += OnPluginEventReceived;
            _connections.Add(connection);
        }

        return _connections;
    }

    public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var connection in _connections)
            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PluginWindowInfo?> GetFirstWindowAsync(CancellationToken cancellationToken = default)
    {
        foreach (var connection in _connections)
        {
            var window = await connection.GetWindowAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (window is not null)
                return window;
        }

        return null;
    }

    public async Task<PluginWindowInfo?> GetWindowAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(x =>
            string.Equals(x.Descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
            return null;

        return await connection.GetWindowAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task BroadcastHostEventAsync(string topic, string payloadJson, CancellationToken cancellationToken = default)
    {
        var hostEvent = new HostEvent
        {
            Topic = topic,
            PayloadJson = payloadJson,
            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        foreach (var connection in _connections)
            await connection.PushHostEventAsync(hostEvent, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            connection.PluginEventReceived -= OnPluginEventReceived;
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _connections.Clear();
    }

    private async Task<PluginDescriptor?> ReadDescriptorAsync(string descriptorPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(descriptorPath);
            var descriptor = await JsonSerializer.DeserializeAsync<PluginDescriptor>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.PluginId) || string.IsNullOrWhiteSpace(descriptor.GrpcEndpoint))
                return null;

            descriptor.SourcePath = descriptorPath;
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read plugin descriptor {Path}", descriptorPath);
            return null;
        }
    }

    private void OnPluginEventReceived(object? sender, PluginEvent e)
    {
        PluginInboundNotification.Publish(e);
        PluginEventReceived?.Invoke(sender ?? this, e);
    }
}
