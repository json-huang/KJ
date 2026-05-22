using KJ.App.Services;
using KJ.Modules.Auth;
using Microsoft.UI.Dispatching;
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
        _navigator = navigator;
        _sessionState = sessionState;
        _container = container;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
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
