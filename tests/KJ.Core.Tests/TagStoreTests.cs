using KJ.Domain.Services;
using FluentAssertions;
using KJ.Domain;
using Xunit;

namespace KJ.Core.Tests;

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

    [Fact]
    public void Upsert_ShouldFireEventOnEachCall()
    {
        var store = new InMemoryTagStore();
        var count = 0;
        store.TagUpdated += (_, _) => Interlocked.Increment(ref count);

        store.Upsert(new TagValue(new TagId("a"), 1, TagQuality.Good, DateTimeOffset.Now));
        store.Upsert(new TagValue(new TagId("a"), 2, TagQuality.Good, DateTimeOffset.Now));

        count.Should().Be(2);
    }

    [Fact]
    public void Upsert_ShouldStoreMultipleTags()
    {
        var store = new InMemoryTagStore();

        store.Upsert(new TagValue(new TagId("a"), 1, TagQuality.Good, DateTimeOffset.Now));
        store.Upsert(new TagValue(new TagId("b"), 2, TagQuality.Good, DateTimeOffset.Now));

        store.TryGet(new TagId("a"), out var a).Should().BeTrue();
        store.TryGet(new TagId("b"), out var b).Should().BeTrue();
        a.Value.Should().Be(1);
        b.Value.Should().Be(2);
    }

    [Fact]
    public void Upsert_NullValue_ShouldStore()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test");

        store.Upsert(new TagValue(id, null, TagQuality.Bad, DateTimeOffset.Now));

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Value.Should().BeNull();
        stored.Quality.Should().Be(TagQuality.Bad);
    }

    [Fact]
    public void Upsert_ShouldPreserveQuality()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test");

        store.Upsert(new TagValue(id, 1, TagQuality.Bad, DateTimeOffset.Now));

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Quality.Should().Be(TagQuality.Bad);
    }
}
