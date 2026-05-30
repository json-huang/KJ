namespace KJ.WinUI.Hosting;

/// <summary>应用级顶层通知（由 <see cref="GlobalNotificationHost"/> 呈现，不占页面布局）。</summary>
public static class GlobalNotification
{
    public static event Action<GlobalNotificationMessage>? ShowRequested;

    public static void Show(string title, string message, TimeSpan? autoDismiss = null) =>
        ShowRequested?.Invoke(new GlobalNotificationMessage(
            title,
            message,
            autoDismiss ?? TimeSpan.FromSeconds(12)));
}

public sealed record GlobalNotificationMessage(string Title, string Message, TimeSpan AutoDismiss);
