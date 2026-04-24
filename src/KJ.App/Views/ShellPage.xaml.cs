using KJ.App.Services;
using KJ.Infrastructure.Auth;
using KJ.Modules.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigator _navigator;
    private readonly ISessionState _sessionState;
    private readonly ILoginCredentialStore _credentialStore;

    public ShellPage(
        INavigator navigator,
        ISessionState sessionState,
        ILoginCredentialStore credentialStore)
    {
        InitializeComponent();
        _navigator = navigator;
        _sessionState = sessionState;
        _credentialStore = credentialStore;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.UiDispatcher ??= DispatcherQueue.GetForCurrentThread();
        await App.WaitForDatabaseInitializationAsync().ConfigureAwait(true);
        _navigator.Attach(RootFrame);
        _ = await TryResumeAsync().ConfigureAwait(true);
        _navigator.GoMain();
    }

    private async Task<bool> TryResumeAsync(CancellationToken cancellationToken = default)
    {
        var (email, password) = _credentialStore.TryLoadStaySignedIn();
        if (string.IsNullOrWhiteSpace(email) || password is null)
            return false;

        // IServiceScopeFactory is registered in the Host service provider (MS DI),
        // not in Prism's DryIoc container.
        var app = (App)Microsoft.UI.Xaml.Application.Current;
        var scopeFactory = app.Host.Services.GetRequiredService<IServiceScopeFactory>();

        await using var scope = scopeFactory.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<ILocalAuthService>();
        var (ok, _) = await auth.SignInAsync(email, password, cancellationToken).ConfigureAwait(true);
        if (!ok)
        {
            _credentialStore.ClearStaySignedIn();
            return false;
        }

        _sessionState.SetSignedIn(email);
        return true;
    }
}
