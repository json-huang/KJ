using KJ.Modules.Auth;

namespace KJ.App.Services;

public sealed class SessionState : ISessionState
{
    public string? Email { get; private set; }
    public bool IsSignedIn => Email is not null;

    public void SetSignedIn(string email) => Email = email;

    public void Clear() => Email = null;
}
