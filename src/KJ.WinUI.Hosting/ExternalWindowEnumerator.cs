using System.Diagnostics;
using System.Text;

namespace KJ.WinUI.Hosting;

public sealed class ExternalWindowEnumerator
{
    public IReadOnlyList<ExternalWindowInfo> Enumerate()
    {
        var currentProcessId = Environment.ProcessId;
        var shellWindow = NativeMethods.GetShellWindow();
        var windows = new List<ExternalWindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!TryCreateWindowInfo(hWnd, currentProcessId, shellWindow, out var info))
                return true;

            windows.Add(info);
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderBy(w => w.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool TryCreateWindowInfo(
        IntPtr hWnd,
        int currentProcessId,
        IntPtr shellWindow,
        out ExternalWindowInfo info)
    {
        info = default!;

        if (hWnd == IntPtr.Zero || hWnd == shellWindow)
            return false;

        if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd))
            return false;

        if (NativeMethods.GetWindow(hWnd, NativeMethods.GwOwner) != IntPtr.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(hWnd, out var processIdValue);
        var processId = unchecked((int)processIdValue);
        if (processId == 0 || processId == currentProcessId)
            return false;

        var title = GetWindowTitle(hWnd);
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var processName = TryGetProcessName(processId);
        info = new ExternalWindowInfo(hWnd, title, processId, processName);
        return true;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLengthW(hWnd);
        if (length <= 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        _ = NativeMethods.GetWindowTextW(hWnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}
