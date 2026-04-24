using KJ.Modules.Auth;

namespace KJ.App.Services;

public sealed class AuthenticationContext : IAuthenticationContext
{
    private readonly ISessionState _sessionState;

    public AuthenticationContext(ISessionState sessionState) => _sessionState = sessionState;

    public bool IsAuthenticated => _sessionState.IsSignedIn;

    public string? PrincipalEmail => _sessionState.Email;
}
