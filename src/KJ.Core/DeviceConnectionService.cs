using KJ.Domain;

namespace KJ.Core;

public sealed class DeviceConnectionService : IDeviceConnectionService
{
    private readonly DevicePollingService _polling;

    public DeviceConnectionService(DevicePollingService polling) => _polling = polling;

    public Task ConnectAsync(string deviceId, CancellationToken ct = default) =>
        _polling.ConnectNowAsync(deviceId, ct);

    public Task DisconnectAsync(string deviceId, CancellationToken ct = default) =>
        _polling.DisconnectNowAsync(deviceId, ct);
}

