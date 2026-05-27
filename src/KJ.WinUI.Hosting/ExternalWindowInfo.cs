namespace KJ.WinUI.Hosting;

public sealed class ExternalWindowInfo
{
    public ExternalWindowInfo(IntPtr handle, string title, int processId, string processName)
    {
        Handle = handle;
        Title = title;
        ProcessId = processId;
        ProcessName = processName;
    }

    public IntPtr Handle { get; set; }

    public string Title { get; set; }

    public int ProcessId { get; set; }

    public string ProcessName { get; set; }

    public string DisplayName => $"{Title}  ({ProcessName}, PID {ProcessId})";
}
