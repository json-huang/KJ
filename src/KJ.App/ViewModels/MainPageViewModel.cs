using KJ.App.Services;
using KJ.Domain.Security;
using KJ.Modules.Auth;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;

namespace KJ.App.ViewModels;

public sealed class MainPageViewModel : BindableBase
{
    private readonly ISessionState _sessionState;
    private readonly INavigator _navigator;
    private readonly ILoginCredentialStore _credentialStore;
    private readonly IDialogService _dialogService;
    private readonly IPermissionService _permissionService;

    public DelegateCommand LogoutCommand { get; }
    public DelegateCommand AboutCommand { get; }

    public string WelcomeText =>
        string.IsNullOrWhiteSpace(_sessionState.Email) ? "未登录" : $"已登录：{_sessionState.Email}";

    public string PermissionHint =>
        _permissionService.HasPermission(Permissions.UserManage) ? "权限：管理员（含用户/角色管理）" : "权限：标准用户（只读类）";

    public MainPageViewModel(
        ISessionState sessionState,
        INavigator navigator,
        ILoginCredentialStore credentialStore,
        IDialogService dialogService,
        IPermissionService permissionService)
    {
        _sessionState = sessionState;
        _navigator = navigator;
        _credentialStore = credentialStore;
        _dialogService = dialogService;
        _permissionService = permissionService;
        LogoutCommand = new DelegateCommand(Logout);
        AboutCommand = new DelegateCommand(ShowAbout);
    }

    private void ShowAbout() =>
        _dialogService.ShowDialog("About", new DialogParameters(), _ => { });

    private void Logout()
    {
        _credentialStore.ClearStaySignedIn();
        _sessionState.Clear();
        RaisePropertyChanged(nameof(WelcomeText));
        RaisePropertyChanged(nameof(PermissionHint));
        _navigator.GoLogin();
    }
}
