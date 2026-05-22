using KJ.App.Services;
using KJ.Modules.Auth;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigator _navigator;
    private readonly ISessionState _sessionState;

    public ShellPage(INavigator navigator, ISessionState sessionState)
    {
        InitializeComponent();
        _navigator = navigator;
        _sessionState = sessionState;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.UiDispatcher ??= DispatcherQueue.GetForCurrentThread();
        await App.WaitForDatabaseInitializationAsync().ConfigureAwait(true);
        _navigator.Attach(RootFrame);

        if (_sessionState.IsSignedIn)
            _navigator.GoMain();
        else
            _navigator.GoLogin();
    }
}
