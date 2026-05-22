using KJ.Infrastructure.Auth;
using KJ.Modules.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.App.Services;

public sealed class SessionResumeService : ISessionResumeService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionState _sessionState;
    private readonly ILoginCredentialStore _credentialStore;

    public SessionResumeService(
        IServiceProvider serviceProvider,
        ISessionState sessionState,
        ILoginCredentialStore credentialStore)
    {
        _serviceProvider = serviceProvider;
        _sessionState = sessionState;
        _credentialStore = credentialStore;
    }

    public async Task<bool> TryResumeAsync(CancellationToken cancellationToken = default)
    {
        var (email, password) = _credentialStore.TryLoadStaySignedIn();
        if (string.IsNullOrWhiteSpace(email) || password is null)
            return false;

        using var scope = _serviceProvider.CreateScope();
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
