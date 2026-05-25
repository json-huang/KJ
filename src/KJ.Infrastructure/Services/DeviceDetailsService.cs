using KJ.Domain;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 设备详情服务。提供单设备的详细信息，包括标签列表、连接历史。
/// </summary>
public sealed class DeviceDetailsService
{
    private readonly IDeviceManager _deviceManager;
    private readonly ITagConfigStore _tagConfigStore;
    private readonly ITagStore _tagStore;
    private readonly IDbContextFactory<KjDbContext>? _dbFactory;

    public DeviceDetailsService(
        IDeviceManager deviceManager,
        ITagConfigStore tagConfigStore,
        ITagStore tagStore,
        IDbContextFactory<KjDbContext>? dbFactory = null)
    {
        _deviceManager = deviceManager;
        _tagConfigStore = tagConfigStore;
        _tagStore = tagStore;
        _dbFactory = dbFactory;
    }

    /// <summary>获取设备详情。</summary>
    public DeviceDetails? GetDetails(string deviceId)
    {
        var device = _deviceManager.GetDevice(deviceId);
        if (device is null) return null;

        var tags = _tagConfigStore.GetTagsForDevice(deviceId);
        var tagDetails = tags.Select(t =>
        {
            _tagStore.TryGet(new TagId(t.TagKey), out var val);
            return new TagDetail
            {
                TagKey = t.TagKey,
                Address = t.Address,
                ValueType = t.ValueType,
                CurrentValue = val.Value,
                Quality = val.Quality,
                LastUpdated = val.Timestamp,
            };
        }).ToList();

        return new DeviceDetails
        {
            DeviceId = device.DeviceId,
            DisplayName = device.DisplayName,
            DriverType = device.DriverType,
            State = device.State,
            Host = device.Host,
            Port = device.Port,
            Tags = tagDetails.AsReadOnly(),
            ConnectedTagCount = tagDetails.Count(t => t.Quality == TagQuality.Good),
            TotalTagCount = tagDetails.Count,
        };
    }

    /// <summary>获取设备的历史连接记录（从 AuditLog 查询）。</summary>
    public async Task<IReadOnlyList<DeviceConnectionRecord>> GetConnectionHistoryAsync(
        string deviceId, int limit = 50, CancellationToken ct = default)
    {
        if (_dbFactory is null) return Array.Empty<DeviceConnectionRecord>();

        try
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var logs = await db.AuditLogs
                .Where(l => l.Details != null && l.Details.Contains(deviceId))
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .Select(l => new DeviceConnectionRecord
                {
                    Timestamp = l.Timestamp,
                    Action = l.Action,
                    Details = l.Details ?? "",
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return logs.AsReadOnly();
        }
        catch
        {
            return Array.Empty<DeviceConnectionRecord>();
        }
    }
}

public sealed class DeviceDetails
{
    public string DeviceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DriverType { get; set; } = "";
    public string State { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public IReadOnlyList<TagDetail> Tags { get; set; } = Array.Empty<TagDetail>();
    public int ConnectedTagCount { get; set; }
    public int TotalTagCount { get; set; }
}

public sealed class TagDetail
{
    public string TagKey { get; set; } = "";
    public string Address { get; set; } = "";
    public TagValueType ValueType { get; set; }
    public object? CurrentValue { get; set; }
    public TagQuality Quality { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

public sealed class DeviceConnectionRecord
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
}
