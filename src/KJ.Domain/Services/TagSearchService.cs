using KJ.Domain;

namespace KJ.Domain.Services;

/// <summary>
/// 标签搜索服务。支持按名称、设备、类型搜索标签。
/// </summary>
public sealed class TagSearchService
{
    private readonly ITagConfigStore _tagConfigStore;
    private readonly ITagStore _tagStore;

    public TagSearchService(ITagConfigStore tagConfigStore, ITagStore tagStore)
    {
        _tagConfigStore = tagConfigStore;
        _tagStore = tagStore;
    }

    /// <summary>搜索标签。支持模糊匹配名称、设备 ID、地址。</summary>
    public IReadOnlyList<TagSearchResult> Search(
        string? keyword = null,
        string? deviceId = null,
        TagValueType? valueType = null,
        int limit = 100)
    {
        // 使用 LINQ 延迟求值，避免中间 List 分配
        IEnumerable<TagConfig> query = _tagConfigStore.GetAllTags();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLowerInvariant();
            query = query.Where(t =>
                t.TagKey.ToLowerInvariant().Contains(kw) ||
                t.Address.ToLowerInvariant().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
            query = query.Where(t => t.DeviceId == deviceId);

        if (valueType.HasValue)
            query = query.Where(t => t.ValueType == valueType.Value);

        return query.Take(limit).Select(t =>
        {
            _tagStore.TryGet(new TagId(t.TagKey), out var currentValue);
            return new TagSearchResult
            {
                TagKey = t.TagKey,
                DeviceId = t.DeviceId,
                Address = t.Address,
                ValueType = t.ValueType,
                CurrentValue = currentValue.Value,
                Quality = currentValue.Quality,
                LastUpdated = currentValue.Timestamp,
            };
        }).ToList().AsReadOnly();
    }

    /// <summary>获取所有标签分组（按设备）。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TagSearchResult>> GroupByDevice()
    {
        var tags = _tagConfigStore.GetAllTags();
        var groups = new Dictionary<string, List<TagSearchResult>>();

        foreach (var t in tags)
        {
            _tagStore.TryGet(new TagId(t.TagKey), out var currentValue);
            var result = new TagSearchResult
            {
                TagKey = t.TagKey,
                DeviceId = t.DeviceId,
                Address = t.Address,
                ValueType = t.ValueType,
                CurrentValue = currentValue.Value,
                Quality = currentValue.Quality,
                LastUpdated = currentValue.Timestamp,
            };

            if (!groups.ContainsKey(t.DeviceId))
                groups[t.DeviceId] = new List<TagSearchResult>();
            groups[t.DeviceId].Add(result);
        }

        return groups.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<TagSearchResult>)kv.Value.AsReadOnly());
    }
}

public sealed class TagSearchResult
{
    public string TagKey { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Address { get; set; } = "";
    public TagValueType ValueType { get; set; }
    public object? CurrentValue { get; set; }
    public TagQuality Quality { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
