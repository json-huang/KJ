using KJ.Domain;
using KJ.Infrastructure.Data.Entities;

namespace KJ.Modules.Monitoring.Services;

/// <summary>
/// 从 IDeviceManager 读取真实设备状态。
/// 替代原来的 NullDeviceStatusProvider。
/// </summary>
public sealed class DeviceStatusProvider : IDeviceStatusProvider
{
    private readonly IDeviceManager _deviceManager;

    public DeviceStatusProvider(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    public bool TryGet(Guid deviceId, out DeviceStatusSnapshot snapshot)
    {
        var device = _deviceManager.GetDevice(deviceId.ToString());
        if (device is null)
        {
            snapshot = default!;
            return false;
        }

        var state = device.State switch
        {
            "Connected" => ConnectionState.Connected,
            "Connecting" => ConnectionState.Connecting,
            "Faulted" => ConnectionState.Faulted,
            _ => ConnectionState.Disconnected,
        };

        snapshot = new DeviceStatusSnapshot(deviceId, state, DateTimeOffset.UtcNow);
        return true;
    }
}
