using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class TagStoreTests
{
    [Fact]
    public void Upsert_ShouldStoreValue()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test.tag");
        var value = new TagValue(id, 42, TagQuality.Good, DateTimeOffset.Now);

        store.Upsert(value);

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Value.Should().Be(42);
    }

    [Fact]
    public void Upsert_ShouldRaiseTagUpdatedEvent()
    {
        var store = new InMemoryTagStore();
        TagValue? received = null;
        store.TagUpdated += (_, v) => received = v;

        var value = new TagValue(new TagId("test.tag"), "hello", TagQuality.Good, DateTimeOffset.Now);
        store.Upsert(value);

        received.HasValue.Should().BeTrue();
        received!.Value.Value.Should().Be("hello");
    }

    [Fact]
    public void TryGet_ShouldReturnFalse_ForMissingTag()
    {
        var store = new InMemoryTagStore();
        store.TryGet(new TagId("nonexistent"), out _).Should().BeFalse();
    }

    [Fact]
    public void Upsert_ShouldOverwriteExisting()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test.tag");

        store.Upsert(new TagValue(id, 1, TagQuality.Good, DateTimeOffset.Now));
        store.Upsert(new TagValue(id, 2, TagQuality.Good, DateTimeOffset.Now));

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Value.Should().Be(2);
    }
}
