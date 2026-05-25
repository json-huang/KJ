using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class TagManagerTests
{
    private static TagConfig MakeTag(string key = "test.tag", string deviceId = "dev1") =>
        new(Guid.NewGuid(), key, deviceId, "HR0", TagValueType.Int32);

    [Fact]
    public void AddTag_ShouldStore()
    {
        var mgr = new TagManager();
        var tag = MakeTag();

        mgr.AddTag(tag);

        mgr.GetAllTags().Should().ContainSingle().Which.TagKey.Should().Be("test.tag");
    }

    [Fact]
    public void AddTag_ShouldThrow_WhenDuplicate()
    {
        var mgr = new TagManager();
        var tag = MakeTag();
        mgr.AddTag(tag);

        var act = () => mgr.AddTag(tag);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddTag_ShouldFireEvent()
    {
        var mgr = new TagManager();
        TagConfig? received = null;
        mgr.TagAdded += (_, t) => received = t;

        var tag = MakeTag();
        mgr.AddTag(tag);

        received.Should().NotBeNull();
        received!.TagKey.Should().Be("test.tag");
    }

    [Fact]
    public void RemoveTag_ShouldRemove()
    {
        var mgr = new TagManager();
        var tag = MakeTag();
        mgr.AddTag(tag);

        mgr.RemoveTag(tag.TagId);

        mgr.GetAllTags().Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_ShouldFireEvent()
    {
        var mgr = new TagManager();
        var tag = MakeTag();
        mgr.AddTag(tag);
        TagConfig? removed = null;
        mgr.TagRemoved += (_, t) => removed = t;

        mgr.RemoveTag(tag.TagId);

        removed.Should().NotBeNull();
        removed!.TagKey.Should().Be("test.tag");
    }

    [Fact]
    public void RemoveTag_ShouldNotThrow_WhenNotFound()
    {
        var mgr = new TagManager();
        var act = () => mgr.RemoveTag(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateTag_ShouldReplace()
    {
        var mgr = new TagManager();
        var tag = MakeTag();
        mgr.AddTag(tag);

        var updated = tag with { Address = "HR100" };
        mgr.UpdateTag(updated);

        mgr.GetTag(tag.TagId)!.Address.Should().Be("HR100");
    }

    [Fact]
    public void UpdateTag_ShouldThrow_WhenNotFound()
    {
        var mgr = new TagManager();
        var act = () => mgr.UpdateTag(MakeTag());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetTagsForDevice_ShouldFilter()
    {
        var mgr = new TagManager();
        mgr.AddTag(MakeTag("a", "dev1"));
        mgr.AddTag(MakeTag("b", "dev2"));
        mgr.AddTag(MakeTag("c", "dev1"));

        mgr.GetTagsForDevice("dev1").Should().HaveCount(2);
    }

    [Fact]
    public void LoadFromStore_ShouldPopulate()
    {
        var mgr = new TagManager();
        var store = new FakeTagConfigStore();
        store.Add(MakeTag("a"));
        store.Add(MakeTag("b"));

        mgr.LoadFromStore(store);

        mgr.GetAllTags().Should().HaveCount(2);
    }

    private sealed class FakeTagConfigStore : ITagConfigStore
    {
        private readonly List<TagConfig> _tags = new();
        public void Add(TagConfig tag) => _tags.Add(tag);
        public IReadOnlyList<TagConfig> GetAllTags() => _tags.AsReadOnly();
        public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId) =>
            _tags.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }
}
