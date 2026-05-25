using KJ.Domain;

namespace KJ.Drivers.Abstractions;

public enum DriverConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Faulted = 3,
}

public sealed record DeviceEndpoint(string Host, int Port, string? Extra = null);

public sealed record TagAddress(string Address, TagValueType Type);

public sealed record TagReadRequest(string TagKey, TagAddress Address);

public sealed record TagWriteRequest(string TagKey, TagAddress Address, object? Value);

public sealed record TagReadResult(string TagKey, object? Value, DateTimeOffset Timestamp, bool Success, string? Error = null);

public interface IDeviceDriver : IAsyncDisposable
{
    string DriverType { get; }
    DriverConnectionState State { get; }

    Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default);
    Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IDeviceDriverFactory
{
    IDeviceDriver Create(string driverType);
    IReadOnlyList<string> GetSupportedDrivers();
}

