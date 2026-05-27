using System.Text;

namespace KJ.Diagnostics;

public sealed class FileDiagnosticSink : IDiagnosticSink, IDisposable
{
    private readonly string _path;
    private readonly object _gate = new();
    private bool _disposed;

    public FileDiagnosticSink(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public void OnEvent(DiagnosticEvent e)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            File.AppendAllText(_path, DiagnosticHub.ToJsonLine(e) + Environment.NewLine, Encoding.UTF8);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}

