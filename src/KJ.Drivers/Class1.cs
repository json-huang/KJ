using Polly;

namespace KJ.Drivers;

public sealed record TransportEndpoint(string Host, int Port);

public interface IDeviceDriver : IAsyncDisposable
{
    string DriverType { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IDeviceDriverFactory
{
    IDeviceDriver Create(string driverType);
}

public sealed class DeviceDriverFactory : IDeviceDriverFactory
{
    private readonly IServiceProvider _services;

    public DeviceDriverFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IDeviceDriver Create(string driverType)
    {
        return driverType switch
        {
            TcpDeviceDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(TcpDeviceDriver))!,
            ModbusTcpDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(ModbusTcpDriver))!,
            OpcUaDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(OpcUaDriver))!,
            _ => throw new NotSupportedException($"Unknown driver type: {driverType}"),
        };
    }
}

public abstract class DeviceDriverBase : IDeviceDriver
{
    protected static readonly ResiliencePipeline DefaultRetry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
        })
        .Build();

    public abstract string DriverType { get; }

    public abstract Task StartAsync(CancellationToken cancellationToken = default);
    public abstract Task StopAsync(CancellationToken cancellationToken = default);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class TcpDeviceDriver : DeviceDriverBase
{
    public const string DriverTypeConst = "Tcp";
    public override string DriverType => DriverTypeConst;

    public override Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class ModbusTcpDriver : DeviceDriverBase
{
    public const string DriverTypeConst = "ModbusTcp";
    public override string DriverType => DriverTypeConst;

    public override Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class OpcUaDriver : DeviceDriverBase
{
    public const string DriverTypeConst = "OpcUa";
    public override string DriverType => DriverTypeConst;

    public override Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
