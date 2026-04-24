using System;

namespace KJ.Modules.Monitoring.Services;

public sealed class NullDeviceStatusProvider : IDeviceStatusProvider
{
    public bool TryGet(Guid deviceId, out DeviceStatusSnapshot snapshot)
    {
        snapshot = default!;
        return false;
    }
}

