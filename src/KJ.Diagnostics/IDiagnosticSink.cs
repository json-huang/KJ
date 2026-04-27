namespace KJ.Diagnostics;

public interface IDiagnosticSink
{
    void OnEvent(DiagnosticEvent e);
}

