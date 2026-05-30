using KJ.Diagnostics;
using KJ.WinUI.Hosting;
using Microsoft.UI.Dispatching;

namespace KJ.App.Services;

public sealed class GlobalNotificationDiagnosticSink : IDiagnosticSink
{
    private readonly DispatcherQueue _dispatcher;
    private readonly string _sourceEquals;
    private readonly TimeSpan _throttleWindow;
    private DateTimeOffset _lastShownAt = DateTimeOffset.MinValue;

    public GlobalNotificationDiagnosticSink(
        DispatcherQueue dispatcher,
        string sourceEquals,
        TimeSpan? throttleWindow = null)
    {
        _dispatcher = dispatcher;
        _sourceEquals = sourceEquals;
        _throttleWindow = throttleWindow ?? TimeSpan.FromSeconds(2);
    }

    public void OnEvent(DiagnosticEvent e)
    {
        if (!string.Equals(e.Source, _sourceEquals, StringComparison.OrdinalIgnoreCase))
            return;

        if (e.Stage is not DiagnosticStage.Exception)
            return;

        var now = DateTimeOffset.Now;
        if (now - _lastShownAt < _throttleWindow)
            return;

        _lastShownAt = now;

        var title = "PLC 连接失败";
        var message = e.Message ?? e.Error ?? "未知错误";
        _ = _dispatcher.TryEnqueue(() => GlobalNotification.Show(title, message));
    }
}

