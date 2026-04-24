using KJ.Comms.Abstractions;
using System.Collections.Concurrent;

namespace KJ.Core;

public sealed class TagStore : ITagStore
{
    private readonly ConcurrentDictionary<string, TagValue> _tags = new();

    public event EventHandler<TagValue>? TagUpdated;

    public bool TryGet(TagId id, out TagValue value) => _tags.TryGetValue(id.Value, out value);

    public void Upsert(TagValue value)
    {
        _tags[value.Id.Value] = value;
        TagUpdated?.Invoke(this, value);
    }
}

