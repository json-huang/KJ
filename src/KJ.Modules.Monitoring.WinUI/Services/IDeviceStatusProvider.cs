namespace KJ.Modules.Monitoring.Services;

public sealed record DeviceStatusSnapshot(
    Guid DeviceId,
    KJ.Infrastructure.Data.Entities.ConnectionState State,
    DateTimeOffset? LastSeenUtc);

public interface IDeviceStatusProvider
{
    bool TryGet(Guid deviceId, out DeviceStatusSnapshot snapshot);
}

