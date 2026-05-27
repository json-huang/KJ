namespace KJ.Modules.Core.Diagnostics;

/// <summary>轻量导航诊断日志（%LocalAppData%\KJ\nav-trace.log）。</summary>
public static class NavTrace
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KJ",
        "nav-trace.log");

    public static void Write(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch
        {
            // best-effort
        }
    }

    public static string LogFilePath => LogPath;
}
