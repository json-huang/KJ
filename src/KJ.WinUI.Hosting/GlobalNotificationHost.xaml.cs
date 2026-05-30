using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.WinUI.Hosting;

public sealed partial class GlobalNotificationHost : UserControl
{
    private DispatcherTimer? _autoDismissTimer;

    public GlobalNotificationHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        GlobalNotification.ShowRequested += OnShowRequested;

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GlobalNotification.ShowRequested -= OnShowRequested;
        _autoDismissTimer?.Stop();
    }

    private void OnShowRequested(GlobalNotificationMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => Present(message));
    }

    private void Present(GlobalNotificationMessage message)
    {
        TitleText.Text = message.Title;
        MessageText.Text = message.Message;
        Banner.Visibility = Visibility.Visible;

        _autoDismissTimer ??= new DispatcherTimer();
        _autoDismissTimer.Interval = message.AutoDismiss;
        _autoDismissTimer.Stop();
        _autoDismissTimer.Tick -= OnAutoDismissTick;
        _autoDismissTimer.Tick += OnAutoDismissTick;
        _autoDismissTimer.Start();
    }

    private void OnAutoDismissTick(object? sender, object e) => Hide();

    private void OnDismissClick(object sender, RoutedEventArgs e) => Hide();

    private void Hide()
    {
        _autoDismissTimer?.Stop();
        Banner.Visibility = Visibility.Collapsed;
    }
}
