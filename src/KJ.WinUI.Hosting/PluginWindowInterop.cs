namespace KJ.WinUI.Hosting;

public static class PluginWindowInterop
{
    public static bool IsWindow(IntPtr hwnd) => NativeMethods.IsWindow(hwnd);

    public static void ShowWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        _ = NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        _ = NativeMethods.ShowWindow(hwnd, NativeMethods.SwShow);
    }

    public static void HideWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        _ = NativeMethods.ShowWindow(hwnd, NativeMethods.SwHide);
    }

    public static void FocusWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        ShowWindow(hwnd);
        _ = NativeMethods.SetForegroundWindow(hwnd);
    }
}
