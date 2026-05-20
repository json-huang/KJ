using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class DeviceManager : IDeviceManager
{
    private readonly ConcurrentDictionary<string, DeviceDescriptor> _devices = new();

    public IReadOnlyList<DeviceDescriptor> ListDevices() =>
        _devices.Values.ToList().AsReadOnly();

    public DeviceDescriptor? GetDevice(string deviceId) =>
        _devices.TryGetValue(deviceId, out var d) ? d : null;

    public void AddDevice(DeviceDescriptor device)
    {
        if (!_devices.TryAdd(device.DeviceId, device))
            throw new InvalidOperationException($"Device '{device.DeviceId}' already exists.");
    }

    public void RemoveDevice(string deviceId) =>
        _devices.TryRemove(deviceId, out _);

    public void UpdateDeviceState(string deviceId, string state)
    {
        _devices.AddOrUpdate(
            deviceId,
            _ => throw new InvalidOperationException($"Device '{deviceId}' not found."),
            (_, existing) => existing with { State = state });
    }
}
