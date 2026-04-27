using System.Text;

namespace KJ.Diagnostics;

public sealed class FileDiagnosticSink : IDiagnosticSink, IDisposable
{
    private readonly string _path;
    private readonly object _gate = new();
    private StreamWriter? _writer;

    public FileDiagnosticSink(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public void OnEvent(DiagnosticEvent e)
    {
        lock (_gate)
        {
            _writer?.WriteLine(DiagnosticHub.ToJsonLine(e));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

