using KJ.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace KJ.Modules.Auth.ViewModels;

public sealed class LoginViewModel : BindableBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionState _sessionState;
    private readonly IRegionManager _regionManager;

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

    public DelegateCommand SignInCommand { get; }

    public LoginViewModel(IServiceScopeFactory scopeFactory, ISessionState sessionState, IRegionManager regionManager)
    {
        _scopeFactory = scopeFactory;
        _sessionState = sessionState;
        _regionManager = regionManager;
        SignInCommand = new DelegateCommand(() => _ = ExecuteSignInAsync());
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
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("HomeOverview", UriKind.Relative));
    }
}

