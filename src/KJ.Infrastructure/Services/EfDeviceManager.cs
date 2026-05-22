using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

public sealed class EfDeviceManager : IDeviceManager
{
    private readonly IDeviceManager _inner;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private bool _loaded;

    public EfDeviceManager(IDeviceManager inner, IDbContextFactory<KjDbContext> dbFactory)
    {
        _inner = inner;
        _dbFactory = dbFactory;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            foreach (var device in db.Devices.AsNoTracking().ToList())
            {
                var descriptor = new DeviceDescriptor(
                    device.Id.ToString(),
                    device.Name,
                    device.Type.ToString(),
                    device.State.ToString());
                try { _inner.AddDevice(descriptor); }
                catch (InvalidOperationException) { }
            }
        }
        catch { }
    }

    public IReadOnlyList<DeviceDescriptor> ListDevices()
    {
        EnsureLoaded();
        return _inner.ListDevices();
    }

    public DeviceDescriptor? GetDevice(string deviceId)
    {
        EnsureLoaded();
        return _inner.GetDevice(deviceId);
    }

    public void AddDevice(DeviceDescriptor device)
    {
        _inner.AddDevice(device);
        _ = Task.Run(() => PersistDevice(device));
    }

    public void RemoveDevice(string deviceId)
    {
        _inner.RemoveDevice(deviceId);
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                if (Guid.TryParse(deviceId, out var guid))
                {
                    var entity = db.Devices.Find(guid);
                    if (entity is not null)
                    {
                        db.Devices.Remove(entity);
                        db.SaveChanges();
                    }
                }
            }
            catch { }
        });
    }

    public void UpdateDeviceState(string deviceId, string state)
    {
        _inner.UpdateDeviceState(deviceId, state);
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                if (Guid.TryParse(deviceId, out var guid))
                {
                    var entity = db.Devices.Find(guid);
                    if (entity is not null && Enum.TryParse<ConnectionState>(state, out var cs))
                    {
                        entity.State = cs;
                        db.SaveChanges();
                    }
                }
            }
            catch { }
        });
    }

    private void PersistDevice(DeviceDescriptor device)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var entity = new Device
            {
                Id = Guid.TryParse(device.DeviceId, out var g) ? g : Guid.NewGuid(),
                Name = device.DisplayName,
                Type = Enum.TryParse<DeviceType>(device.DriverType, out var dt) ? dt : DeviceType.Plc,
                State = Enum.TryParse<ConnectionState>(device.State, out var cs) ? cs : ConnectionState.Disconnected,
            };
            db.Devices.Add(entity);
            db.SaveChanges();
        }
        catch { }
    }
}
