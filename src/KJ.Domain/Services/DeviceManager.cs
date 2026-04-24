namespace KJ.Domain.Services;

public sealed class DeviceManager : IDeviceManager
{
    private readonly List<DeviceDescriptor> _devices = new()
    {
        new DeviceDescriptor("SimDevice1", "Simulated Device 1", "Sim")
    };

    public IReadOnlyList<DeviceDescriptor> ListDevices() => _devices;
}

