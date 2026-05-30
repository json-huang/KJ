using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace KJ.WinUI.Hosting;

/// <summary>
/// 监听系统窗口移动结束事件：当用户把窗口拖到目标区域并松开鼠标时触发回调。
/// </summary>
public sealed class WindowMoveDropEmbedService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<Rect?> _getDropRectInScreenPixels;
    private readonly Action<ExternalWindowInfo> _onDropped;
    private IntPtr _hook;
    private NativeMethods.WinEventProc? _proc;

    public WindowMoveDropEmbedService(
        DispatcherQueue dispatcher,
        Func<Rect?> getDropRectInScreenPixels,
        Action<ExternalWindowInfo> onDropped)
    {
        _dispatcher = dispatcher;
        _getDropRectInScreenPixels = getDropRectInScreenPixels;
        _onDropped = onDropped;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
            return;

        _proc = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemMoveSizeEnd,
            NativeMethods.EventSystemMoveSizeEnd,
            IntPtr.Zero,
            _proc,
            0,
            0,
            NativeMethods.WineventOutOfContext);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
            return;

        _ = NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
        _proc = null;
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        var dropRect = _getDropRectInScreenPixels();
        if (dropRect is null)
            return;

        var cursor = new Point(pt.X, pt.Y);
        if (!dropRect.Value.Contains(cursor))
            return;

        _ = _dispatcher.TryEnqueue(() =>
        {
            // root/top-level
            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot);
            if (root != IntPtr.Zero)
                hwnd = root;

            if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
                return;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pidU);
            var pid = unchecked((int)pidU);
            if (pid == 0 || pid == Environment.ProcessId)
                return;

            if (!ExternalWindowPicker.TryPickFromCursor(out var info))
                return;

            // Ensure we embed the moved window, not whatever is currently under cursor if focus changed.
            info.Handle = hwnd;
            info.ProcessId = pid;
            _onDropped(info);
        });
    }

    public void Dispose() => Stop();
}

