using KJ.Infrastructure.Auth;
using KJ.Modules.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.App.Services;

public sealed class SessionResumeService : ISessionResumeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionState _sessionState;
    private readonly INavigator _navigator;
    private readonly ILoginCredentialStore _credentialStore;

    public SessionResumeService(
        IServiceScopeFactory scopeFactory,
        ISessionState sessionState,
        INavigator navigator,
        ILoginCredentialStore credentialStore)
    {
        _scopeFactory = scopeFactory;
        _sessionState = sessionState;
        _navigator = navigator;
        _credentialStore = credentialStore;
    }

    public async Task<bool> TryResumeAsync(CancellationToken cancellationToken = default)
    {
        var (email, password) = _credentialStore.TryLoadStaySignedIn();
        if (string.IsNullOrWhiteSpace(email) || password is null)
            return false;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<ILocalAuthService>();
        var (ok, _) = await auth.SignInAsync(email, password, cancellationToken).ConfigureAwait(true);
        if (!ok)
        {
            _credentialStore.ClearStaySignedIn();
            return false;
        }

        _sessionState.SetSignedIn(email);
        _navigator.GoMain();
        return true;
    }
}
