using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace KJ.App.Services;

internal static class WindowChromeHelper
{
    private const int SwMinimize = 6;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void Minimize(Window? window)
    {
        if (window is null)
            return;

        ShowWindow(WindowNative.GetWindowHandle(window), SwMinimize);
    }
}
