using KJ.Domain;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 从数据库读取标签配置，提供给 DevicePollingService 使用。
/// 首次访问时加载全部标签配置到内存缓存，后续直接返回缓存。
/// </summary>
public sealed class EfTagConfigStore : ITagConfigStore
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private List<TagConfig>? _cache;
    private readonly object _gate = new();

    public EfTagConfigStore(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public IReadOnlyList<TagConfig> GetAllTags()
    {
        EnsureLoaded();
        return _cache!;
    }

    public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId)
    {
        EnsureLoaded();
        return _cache!.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }

    /// <summary>强制刷新缓存（设备/标签配置变更后调用）。</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _cache = null;
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null) return;

        lock (_gate)
        {
            if (_cache is not null) return;

            try
            {
                using var db = _dbFactory.CreateDbContext();
                var entities = db.Tags.AsNoTracking().ToList();
                _cache = entities.Select(t => new TagConfig(
                    TagId: t.Id,
                    TagKey: t.Name,
                    DeviceId: t.DeviceId.ToString(),
                    Address: t.Address,
                    ValueType: MapDataType(t.DataType),
                    PollIntervalMs: t.PollIntervalMs > 0 ? t.PollIntervalMs : 1000)).ToList();
            }
            catch
            {
                _cache = new List<TagConfig>();
            }
        }
    }

    private static TagValueType MapDataType(Data.Entities.TagDataType type) => type switch
    {
        Data.Entities.TagDataType.Bool => TagValueType.Bool,
        Data.Entities.TagDataType.Int32 => TagValueType.Int32,
        Data.Entities.TagDataType.Int64 => TagValueType.Int64,
        Data.Entities.TagDataType.Float => TagValueType.Float,
        Data.Entities.TagDataType.Double => TagValueType.Double,
        Data.Entities.TagDataType.String => TagValueType.String,
        Data.Entities.TagDataType.Bytes => TagValueType.Bytes,
        _ => TagValueType.Int32,
    };
}
