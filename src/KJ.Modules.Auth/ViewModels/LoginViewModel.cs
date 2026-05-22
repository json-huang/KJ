using KJ.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Auth.ViewModels;

public sealed class LoginViewModel : BindableBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionState _sessionState;
    private readonly INavigator _navigator;
    private readonly ILoginCredentialStore _credentialStore;

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password { get; set; } = string.Empty;

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private bool _rememberEmail;
    public bool RememberEmail
    {
        get => _rememberEmail;
        set => SetProperty(ref _rememberEmail, value);
    }

    private bool _staySignedIn;
    public bool StaySignedIn
    {
        get => _staySignedIn;
        set => SetProperty(ref _staySignedIn, value);
    }

    public DelegateCommand SignInCommand { get; }

    public LoginViewModel(
        IServiceScopeFactory scopeFactory,
        ISessionState sessionState,
        INavigator navigator,
        ILoginCredentialStore credentialStore)
    {
        _scopeFactory = scopeFactory;
        _sessionState = sessionState;
        _navigator = navigator;
        _credentialStore = credentialStore;
        SignInCommand = new DelegateCommand(() => _ = ExecuteSignInAsync());

        var remembered = _credentialStore.LoadRememberedEmail();
        if (!string.IsNullOrWhiteSpace(remembered))
        {
            Email = remembered;
            RememberEmail = true;
        }
        else
        {
            RememberEmail = false;
        }
    }

    private async Task ExecuteSignInAsync()
    {
        ErrorMessage = string.Empty;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<ILocalAuthService>();
        var (ok, err) = await auth.SignInAsync(Email, Password).ConfigureAwait(true);
        if (!ok)
        {
            ErrorMessage = err ?? "登录失败。";
            return;
        }

        _sessionState.SetSignedIn(Email.Trim());

        if (RememberEmail)
            _credentialStore.SaveRememberedEmail(Email.Trim());
        else
            _credentialStore.ClearRememberedEmail();

        if (StaySignedIn)
            _credentialStore.SaveStaySignedIn(Email.Trim(), Password);
        else
            _credentialStore.ClearStaySignedIn();

        _navigator.GoMain();
    }
}
