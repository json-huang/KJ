using System.Collections.Concurrent;
using System.Text.Json;

namespace KJ.Diagnostics;

public sealed class DiagnosticHub
{
    private readonly ConcurrentQueue<DiagnosticEvent> _buffer = new();
    private readonly List<IDiagnosticSink> _sinks = new();
    private readonly int _maxEvents;

    public DiagnosticHub(int maxEvents = 2000)
    {
        _maxEvents = Math.Max(100, maxEvents);
    }

    public void AddSink(IDiagnosticSink sink)
    {
        lock (_sinks)
            _sinks.Add(sink);
    }

    public void Publish(DiagnosticEvent e)
    {
        _buffer.Enqueue(e);
        while (_buffer.Count > _maxEvents && _buffer.TryDequeue(out _))
        {
        }

        IDiagnosticSink[] sinks;
        lock (_sinks)
            sinks = _sinks.ToArray();

        foreach (var s in sinks)
        {
            try { s.OnEvent(e); }
            catch { /* best-effort */ }
        }
    }

    public IReadOnlyList<DiagnosticEvent> Snapshot()
    {
        return _buffer.ToArray();
    }

    public static string ToJsonLine(DiagnosticEvent e) =>
        JsonSerializer.Serialize(e);
}

