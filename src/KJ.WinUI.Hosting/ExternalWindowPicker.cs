using System.Diagnostics;

namespace KJ.WinUI.Hosting;

public static class ExternalWindowPicker
{
    /// <summary>
    /// Picks the top-level window under current cursor position.
    /// </summary>
    public static bool TryPickFromCursor(out ExternalWindowInfo window)
    {
        window = null!;

        if (!NativeMethods.GetCursorPos(out var pt))
            return false;

        var h = NativeMethods.WindowFromPoint(pt);
        if (h == IntPtr.Zero || !NativeMethods.IsWindow(h))
            return false;

        var root = NativeMethods.GetAncestor(h, NativeMethods.GaRoot);
        if (root != IntPtr.Zero)
            h = root;

        if (!NativeMethods.IsWindowVisible(h))
            return false;

        NativeMethods.GetWindowThreadProcessId(h, out var pidU);
        var pid = unchecked((int)pidU);
        if (pid == 0 || pid == Environment.ProcessId)
            return false;

        var title = TryGetTitle(h);
        if (string.IsNullOrWhiteSpace(title))
            title = $"HWND 0x{h.ToInt64():X}";

        var processName = TryGetProcessName(pid);
        window = new ExternalWindowInfo(h, title, pid, processName);
        return true;
    }

    private static string TryGetTitle(IntPtr hWnd)
    {
        try
        {
            var len = NativeMethods.GetWindowTextLengthW(hWnd);
            if (len <= 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder(len + 1);
            _ = NativeMethods.GetWindowTextW(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryGetProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}

