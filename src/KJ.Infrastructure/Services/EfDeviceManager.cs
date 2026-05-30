using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KJ.Infrastructure.Services;

public sealed class EfDeviceManager : IDeviceManager
{
    private readonly IDeviceManager _inner;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly ILogger<EfDeviceManager>? _logger;
    private bool _loaded;

    public EfDeviceManager(
        IDeviceManager inner,
        IDbContextFactory<KjDbContext> dbFactory,
        ILogger<EfDeviceManager>? logger = null)
    {
        _inner = inner;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            foreach (var device in db.Devices.AsNoTracking().ToList())
            {
                string deviceId = device.Id.ToString();
                string driverType = device.Type.ToString();
                string? extra = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(device.PropertiesJson))
                    {
                        using var doc = JsonDocument.Parse(device.PropertiesJson);
                        if (doc.RootElement.TryGetProperty("deviceId", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                            deviceId = idProp.GetString() ?? deviceId;
                        if (doc.RootElement.TryGetProperty("driverType", out var dtProp) && dtProp.ValueKind == JsonValueKind.String)
                            driverType = dtProp.GetString() ?? driverType;
                        if (doc.RootElement.TryGetProperty("extra", out var exProp) && exProp.ValueKind == JsonValueKind.String)
                            extra = exProp.GetString();
                    }
                }
                catch
                {
                    // ignore invalid json
                }

                var descriptor = new DeviceDescriptor(
                    deviceId,
                    device.Name,
                    driverType,
                    device.State.ToString(),
                    Host: device.Address?.Host ?? "",
                    Port: device.Address?.Port ?? 0,
                    Extra: extra);
                try { _inner.AddDevice(descriptor); }
                catch (InvalidOperationException) { /* 设备已存在，跳过 */ }
            }

            _loaded = true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load devices from database");
        }
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
        PersistDeviceAsync(device);
    }

    public void RemoveDevice(string deviceId)
    {
        _inner.RemoveDevice(deviceId);
        RemoveDeviceAsync(deviceId);
    }

    public void UpdateDeviceState(string deviceId, string state)
    {
        _inner.UpdateDeviceState(deviceId, state);
        UpdateDeviceStateAsync(deviceId, state);
    }

    // 使用 async void + try-catch 做 fire-and-forget 写入
    // 生产环境应改为后台队列服务
    private async void PersistDeviceAsync(DeviceDescriptor device)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var entity = new Device
            {
                Id = BuildStableGuid(device.DeviceId),
                Name = device.DisplayName,
                Type = Enum.TryParse<DeviceType>(device.DriverType, out var dt) ? dt : DeviceType.Plc,
                State = Enum.TryParse<ConnectionState>(device.State, out var cs) ? cs : ConnectionState.Disconnected,
                Address = new DeviceAddress
                {
                    Host = device.Host ?? "",
                    Port = device.Port,
                },
                PropertiesJson = JsonSerializer.Serialize(new
                {
                    deviceId = device.DeviceId,
                    driverType = device.DriverType,
                    extra = device.Extra,
                })
            };
            // Upsert by Id (stable guid)
            var existing = await db.Devices.FindAsync(entity.Id).ConfigureAwait(false);
            if (existing is null)
            {
                db.Devices.Add(entity);
            }
            else
            {
                existing.Name = entity.Name;
                existing.Type = entity.Type;
                existing.State = entity.State;
                existing.Address.Host = entity.Address.Host;
                existing.Address.Port = entity.Address.Port;
                existing.PropertiesJson = entity.PropertiesJson;
            }
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist device {DeviceId}", device.DeviceId);
        }
    }

    private async void RemoveDeviceAsync(string deviceId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var guid = BuildStableGuid(deviceId);
            var entity = await db.Devices.FindAsync(guid).ConfigureAwait(false);
            if (entity is not null)
            {
                db.Devices.Remove(entity);
                await db.SaveChangesAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove device {DeviceId} from database", deviceId);
        }
    }

    private async void UpdateDeviceStateAsync(string deviceId, string state)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var guid = BuildStableGuid(deviceId);
            var entity = await db.Devices.FindAsync(guid).ConfigureAwait(false);
            if (entity is not null && Enum.TryParse<ConnectionState>(state, out var cs))
            {
                entity.State = cs;
                await db.SaveChangesAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update device {DeviceId} state", deviceId);
        }
    }

    private static Guid BuildStableGuid(string deviceId)
    {
        if (Guid.TryParse(deviceId, out var g))
            return g;

        // stable GUID derived from deviceId string
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(deviceId));
        return new Guid(bytes);
    }
}
