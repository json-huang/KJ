using KJ.Domain;

namespace KJ.Domain.Services;

/// <summary>
/// 标签管理服务。提供标签的 CRUD 操作，与 DeviceManager 配合使用。
/// </summary>
public sealed class TagManager
{
    private readonly List<TagConfig> _tags = new();
    private readonly object _gate = new();

    public event EventHandler<TagConfig>? TagAdded;
    public event EventHandler<TagConfig>? TagRemoved;
    public event EventHandler<TagConfig>? TagUpdated;

    public IReadOnlyList<TagConfig> GetAllTags()
    {
        lock (_gate) return _tags.ToList().AsReadOnly();
    }

    public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId)
    {
        lock (_gate) return _tags.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }

    public TagConfig? GetTag(Guid tagId)
    {
        lock (_gate) return _tags.FirstOrDefault(t => t.TagId == tagId);
    }

    public void AddTag(TagConfig tag)
    {
        lock (_gate)
        {
            if (_tags.Any(t => t.TagId == tag.TagId))
                throw new InvalidOperationException($"Tag '{tag.TagKey}' already exists.");
            _tags.Add(tag);
        }
        TagAdded?.Invoke(this, tag);
    }

    public void UpdateTag(TagConfig tag)
    {
        lock (_gate)
        {
            var index = _tags.FindIndex(t => t.TagId == tag.TagId);
            if (index < 0)
                throw new InvalidOperationException($"Tag '{tag.TagKey}' not found.");
            _tags[index] = tag;
        }
        TagUpdated?.Invoke(this, tag);
    }

    public void RemoveTag(Guid tagId)
    {
        TagConfig? removed;
        lock (_gate)
        {
            removed = _tags.FirstOrDefault(t => t.TagId == tagId);
            if (removed is null) return;
            _tags.Remove(removed);
        }
        TagRemoved?.Invoke(this, removed);
    }

    public void LoadFromStore(ITagConfigStore store)
    {
        lock (_gate)
        {
            _tags.Clear();
            _tags.AddRange(store.GetAllTags());
        }
    }
}
