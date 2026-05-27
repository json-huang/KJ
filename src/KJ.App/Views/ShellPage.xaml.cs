using KJ.App.Services;
using KJ.Modules.Auth;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Prism.Ioc;

namespace KJ.App.Views;

public sealed partial class ShellPage : Page
{
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
        Loaded += OnLoaded;

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

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        UpdateMaximizeToolTip();
        App.UiDispatcher ??= DispatcherQueue.GetForCurrentThread();
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
