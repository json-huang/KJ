using KJ.App.Services;
using KJ.Modules.Auth;
using KJ.Modules.Monitoring.Workflows;
using KJ.Plugin.Host;
using KJ.WinUI.Hosting;
using KJ.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Prism.Ioc;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace KJ.App.Views;

public sealed partial class ShellPage : Page
{
    private static int _diagnosticsSinkHooked;
    private readonly INavigator _navigator;
    private readonly ISessionState _sessionState;
    private readonly IContainerProvider _container;

    public ShellPage(INavigator navigator, ISessionState sessionState, IContainerProvider container)
    {
        InitializeComponent();
        App.MainWindow?.SetTitleBar(AppTitleBarDragRegion);
        _navigator = navigator;
        _sessionState = sessionState;
        _container = container;
        PluginInboundNotification.Received += OnPluginInboundReceived;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        if (App.MainWindow?.AppWindow is { } appWindow)
            appWindow.Changed += OnAppWindowChanged;
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange)
            UpdateMaximizeToolTip();
    }

    private void UpdateMaximizeToolTip()
    {
        var maximized = App.MainWindow?.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        ToolTipService.SetToolTip(MaximizeWindowButton, maximized == true ? "还原" : "最大化");
    }

    private void MinimizeWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        WindowChromeHelper.Minimize(App.MainWindow);

    private void MaximizeWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (App.MainWindow?.AppWindow.Presenter is not OverlappedPresenter presenter)
            return;

        if (presenter.State == OverlappedPresenterState.Maximized)
            presenter.Restore();
        else
            presenter.Maximize();

        UpdateMaximizeToolTip();
    }

    private void CloseWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        App.MainWindow?.Close();

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        PluginInboundNotification.Received -= OnPluginInboundReceived;

    private void OnPluginInboundReceived(Plugin.Contracts.PluginEvent pluginEvent)
    {
        if (!PluginEventDisplay.ShouldNotify(pluginEvent))
            return;

        var (title, message) = PluginEventDisplay.Format(pluginEvent);
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            GlobalNotification.Show(title, message);
            WorkflowAppServices.ActivateMainWindow?.Invoke();
        });
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        UpdateMaximizeToolTip();
        App.UiDispatcher ??= DispatcherQueue.GetForCurrentThread();

        // 全局诊断通知（连接失败等）——只挂一次，避免重复弹窗
        if (Interlocked.Exchange(ref _diagnosticsSinkHooked, 1) == 0 && App.UiDispatcher is { } dq)
        {
            try
            {
                var scopeFactory = _container.Resolve<IServiceScopeFactory>();
                using var scope = scopeFactory.CreateScope();
                var hub = scope.ServiceProvider.GetRequiredService<DiagnosticHub>();

                // 同时落一份本地日志，方便拷贝排查
                var logPath = Path.Combine(Path.GetTempPath(), "KJ.App-ads-connect.log");
                try
                {
                    if (!File.Exists(logPath))
                        File.WriteAllText(logPath, string.Empty);
                }
                catch
                {
                    // ignore
                }
                hub.AddSink(new FileDiagnosticSink(logPath));
                hub.AddSink(new Services.GlobalNotificationDiagnosticSink(
                    dq,
                    sourceEquals: nameof(KJ.Drivers.Plc.Beckhoff.Ads.BeckhoffAdsDriver)));
            }
            catch
            {
                // ignore (best-effort)
            }
        }

        await App.WaitForDatabaseInitializationAsync().ConfigureAwait(true);
        _navigator.Attach(RootFrame);

        if (_sessionState.IsSignedIn)
        {
            _navigator.GoMain();
            return;
        }

        try
        {
            var resumeService = _container.Resolve<ISessionResumeService>();
            await resumeService.TryResumeAsync().ConfigureAwait(true);
        }
        catch
        {
            // Resume service unavailable — fall through to login
        }

        if (_sessionState.IsSignedIn)
            _navigator.GoMain();
        else
            _navigator.GoLogin();
    }
}
