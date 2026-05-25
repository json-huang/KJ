using FluentAssertions;
using Xunit;

namespace KJ.Diagnostics.Tests;

public class DiagnosticHubTests
{
    private static DiagnosticEvent MakeEvent(string? message = null, string? traceId = null) =>
        new(DateTimeOffset.Now, traceId ?? Guid.NewGuid().ToString("N"),
            DiagnosticStage.DriverRead, "Test", Message: message);

    // ── Publish / Snapshot ────────────────────────────────────────────────

    [Fact]
    public void Publish_ShouldStoreEvent()
    {
        var hub = new DiagnosticHub();
        hub.Publish(MakeEvent("hello"));

        hub.Snapshot().Should().HaveCount(1);
        hub.Snapshot()[0].Message.Should().Be("hello");
    }

    [Fact]
    public void Snapshot_ShouldReturnEmpty_WhenNoEvents()
    {
        var hub = new DiagnosticHub();
        hub.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Publish_MultipleEvents_ShouldStoreAll()
    {
        var hub = new DiagnosticHub();
        hub.Publish(MakeEvent("a"));
        hub.Publish(MakeEvent("b"));
        hub.Publish(MakeEvent("c"));

        hub.Snapshot().Should().HaveCount(3);
    }

    [Fact]
    public void Buffer_ShouldEvictOldest_WhenMaxExceeded()
    {
        var hub = new DiagnosticHub(maxEvents: 100);
        for (int i = 0; i < 110; i++)
            hub.Publish(MakeEvent($"msg{i}"));

        var snapshot = hub.Snapshot();
        snapshot.Should().HaveCount(100);
        snapshot[0].Message.Should().Be("msg10"); // oldest kept
        snapshot[99].Message.Should().Be("msg109"); // newest
    }

    [Fact]
    public void MaxEvents_ShouldClampTo100_Minimum()
    {
        // Constructor clamps to max(100, maxEvents)
        var hub = new DiagnosticHub(maxEvents: 10);
        for (int i = 0; i < 150; i++)
            hub.Publish(MakeEvent($"msg{i}"));

        hub.Snapshot().Should().HaveCount(100);
    }

    // ── Sinks ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddSink_ShouldReceivePublishedEvents()
    {
        var hub = new DiagnosticHub();
        var sink = new CollectingSink();
        hub.AddSink(sink);

        hub.Publish(MakeEvent("test"));

        sink.Events.Should().HaveCount(1);
        sink.Events[0].Message.Should().Be("test");
    }

    [Fact]
    public void MultipleSinks_ShouldAllReceiveEvents()
    {
        var hub = new DiagnosticHub();
        var sink1 = new CollectingSink();
        var sink2 = new CollectingSink();
        hub.AddSink(sink1);
        hub.AddSink(sink2);

        hub.Publish(MakeEvent("test"));

        sink1.Events.Should().HaveCount(1);
        sink2.Events.Should().HaveCount(1);
    }

    [Fact]
    public void SinkException_ShouldNotPropagate()
    {
        var hub = new DiagnosticHub();
        hub.AddSink(new ThrowingSink());
        hub.AddSink(new CollectingSink());

        var act = () => hub.Publish(MakeEvent("test"));
        act.Should().NotThrow();
    }

    [Fact]
    public void SinkException_ShouldStillDeliverToOtherSinks()
    {
        var hub = new DiagnosticHub();
        hub.AddSink(new ThrowingSink());
        var good = new CollectingSink();
        hub.AddSink(good);

        hub.Publish(MakeEvent("test"));

        good.Events.Should().HaveCount(1);
    }

    // ── ToJsonLine ────────────────────────────────────────────────────────

    [Fact]
    public void ToJsonLine_ShouldProduceValidJson()
    {
        var evt = MakeEvent("hello");
        var json = DiagnosticHub.ToJsonLine(evt);

        json.Should().Contain("hello");
        json.Should().Contain("Source");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private sealed class CollectingSink : IDiagnosticSink
    {
        public List<DiagnosticEvent> Events { get; } = new();
        public void OnEvent(DiagnosticEvent e) => Events.Add(e);
    }

    private sealed class ThrowingSink : IDiagnosticSink
    {
        public void OnEvent(DiagnosticEvent e) => throw new Exception("sink error");
    }
}

public class FileDiagnosticSinkTests : IDisposable
{
    private readonly string _tempDir;

    public FileDiagnosticSinkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"kj_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static DiagnosticEvent MakeEvent(string? message = null) =>
        new(DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
            DiagnosticStage.DriverRead, "Test", Message: message);

    [Fact]
    public void OnEvent_ShouldWriteToFile()
    {
        var path = Path.Combine(_tempDir, "diag.log");
        using var sink = new FileDiagnosticSink(path);

        sink.OnEvent(MakeEvent("hello"));

        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("hello");
    }

    [Fact]
    public void OnEvent_MultipleEvents_ShouldWriteAll()
    {
        var path = Path.Combine(_tempDir, "diag.log");
        using var sink = new FileDiagnosticSink(path);

        sink.OnEvent(MakeEvent("a"));
        sink.OnEvent(MakeEvent("b"));

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public void Dispose_ShouldPreventFurtherWrites()
    {
        var path = Path.Combine(_tempDir, "diag.log");
        var sink = new FileDiagnosticSink(path);
        sink.OnEvent(MakeEvent("before"));
        sink.Dispose();

        // After dispose, writer is null - should not throw
        var act = () => sink.OnEvent(MakeEvent("after"));
        act.Should().NotThrow();

        var content = File.ReadAllText(path);
        content.Should().Contain("before");
        content.Should().NotContain("after");
    }

    [Fact]
    public void Constructor_ShouldCreateDirectory()
    {
        var path = Path.Combine(_tempDir, "sub", "dir", "diag.log");
        using var sink = new FileDiagnosticSink(path);

        Directory.Exists(Path.GetDirectoryName(path)!).Should().BeTrue();
    }
}
