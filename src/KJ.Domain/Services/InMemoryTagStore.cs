using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class InMemoryTagStore : ITagStore
{
    private readonly ConcurrentDictionary<string, TagValue> _tags = new();

    public event EventHandler<TagValue>? TagUpdated;

    public bool TryGet(TagId id, out TagValue value) => _tags.TryGetValue(id.Value, out value);

    public int Count => _tags.Count;

    public void Upsert(TagValue value)
    {
        _tags[value.Id.Value] = value;
        TagUpdated?.Invoke(this, value);
    }
}

