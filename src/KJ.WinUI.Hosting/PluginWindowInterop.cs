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

    /// <summary>释放嵌入后恢复为独立顶层窗口（任务栏可见）。</summary>
    public static void RestoreStandaloneWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        _ = NativeMethods.SetParent(hwnd, IntPtr.Zero);

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64();
        style &= ~NativeMethods.WsChild;
        style |= NativeMethods.WsPopup |
                  NativeMethods.WsCaption |
                  NativeMethods.WsThickFrame |
                  NativeMethods.WsSysMenu |
                  NativeMethods.WsMinimizeBox |
                  NativeMethods.WsMaximizeBox |
                  NativeMethods.WsVisible;

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        exStyle |= NativeMethods.WsExAppWindow;
        exStyle &= ~NativeMethods.WsExToolWindow;

        _ = NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, new IntPtr(style));
        _ = NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(exStyle));
        _ = NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow);

        ShowWindow(hwnd);
        _ = NativeMethods.SetForegroundWindow(hwnd);
    }
}
