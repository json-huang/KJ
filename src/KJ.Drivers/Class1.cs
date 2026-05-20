using System.Net.Sockets;
using KJ.Diagnostics;
using KJ.Drivers.Abstractions;
using Polly;

namespace KJ.Drivers;

public sealed class TcpDeviceDriver : IDeviceDriver
{
    public const string DriverTypeConst = "Tcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly DiagnosticHub _diagnostics;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.FromMilliseconds(200) })
        .Build();

    private static readonly ResiliencePipeline CircuitBreaker = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
        })
        .Build();

    public TcpDeviceDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connecting;
        _client = new TcpClient();
        await _client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
        State = DriverConnectionState.Connected;
        _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
            DiagnosticStage.TransportOpen, "TcpDriver",
            Message: $"Connected to {endpoint.Host}:{endpoint.Port}"));
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                return await Retry.ExecuteAsync(async ct2 =>
                {
                    var addressBytes = System.Text.Encoding.UTF8.GetBytes(request.Address.Address);
                    await _stream.WriteAsync(addressBytes, ct2).ConfigureAwait(false);
                    var buffer = new byte[4096];
                    var read = await _stream.ReadAsync(buffer, ct2).ConfigureAwait(false);
                    var value = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                    return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            throw new InvalidOperationException("Not connected");

        var data = System.Text.Encoding.UTF8.GetBytes(request.Value?.ToString() ?? string.Empty);
        await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}

public sealed class ModbusTcpDriver : IDeviceDriver
{
    public const string DriverTypeConst = "ModbusTcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly DiagnosticHub _diagnostics;

    public ModbusTcpDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connected;
        _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
            DiagnosticStage.TransportOpen, "ModbusTcpDriver",
            Message: $"Connected to {endpoint.Host}:{endpoint.Port}"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TagReadResult(request.TagKey, 0, DateTimeOffset.Now, true));
    }

    public Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class OpcUaDriver : IDeviceDriver
{
    public const string DriverTypeConst = "OpcUa";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly DiagnosticHub _diagnostics;

    public OpcUaDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connected;
        _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
            DiagnosticStage.TransportOpen, "OpcUaDriver",
            Message: $"Connected to {endpoint.Host}:{endpoint.Port}"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TagReadResult(request.TagKey, null, DateTimeOffset.Now, true));

    public Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class DeviceDriverFactory : IDeviceDriverFactory
{
    private readonly IServiceProvider _services;

    public DeviceDriverFactory(IServiceProvider services) => _services = services;

    public IDeviceDriver Create(string driverType) => driverType switch
    {
        TcpDeviceDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(TcpDeviceDriver))!,
        ModbusTcpDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(ModbusTcpDriver))!,
        OpcUaDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(OpcUaDriver))!,
        _ => throw new NotSupportedException($"Unknown driver type: {driverType}"),
    };

    public IReadOnlyList<string> GetSupportedDrivers() =>
        new[] { TcpDeviceDriver.DriverTypeConst, ModbusTcpDriver.DriverTypeConst, OpcUaDriver.DriverTypeConst };
}
