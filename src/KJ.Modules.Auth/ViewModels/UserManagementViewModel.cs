using System.Collections.ObjectModel;
using KJ.Domain.Identity;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Auth.ViewModels;

public sealed class UserManagementViewModel : BindableBase
{
    private readonly IUserManager _userManager;

    public ObservableCollection<AppUser> Users { get; } = new();

    private string _newUsername = string.Empty;
    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    private string _newEmail = string.Empty;
    public string NewEmail
    {
        get => _newEmail;
        set => SetProperty(ref _newEmail, value);
    }

    private string _newPassword = string.Empty;
    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public DelegateCommand LoadUsersCommand { get; }
    public DelegateCommand CreateUserCommand { get; }
    public DelegateCommand<AppUser> DeleteUserCommand { get; }

    public UserManagementViewModel(IUserManager userManager)
    {
        _userManager = userManager;
        LoadUsersCommand = new DelegateCommand(() => _ = ExecuteLoadUsersAsync());
        CreateUserCommand = new DelegateCommand(() => _ = ExecuteCreateUserAsync());
        DeleteUserCommand = new DelegateCommand<AppUser>(u => { if (u is not null) _ = ExecuteDeleteUserAsync(u); });

        _ = ExecuteLoadUsersAsync();
    }

    private async Task ExecuteLoadUsersAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var users = await _userManager.GetUsersAsync().ConfigureAwait(true);
            Users.Clear();
            foreach (var u in users)
                Users.Add(u);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载用户失败：{ex.Message}";
        }
    }

    private async Task ExecuteCreateUserAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewEmail) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "请填写所有字段。";
            return;
        }

        try
        {
            var user = new AppUser(string.Empty, NewUsername.Trim(), NewEmail.Trim());
            await _userManager.CreateUserAsync(user, NewPassword).ConfigureAwait(true);
            NewUsername = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            await ExecuteLoadUsersAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"创建用户失败：{ex.Message}";
        }
    }

    private async Task ExecuteDeleteUserAsync(AppUser user)
    {
        ErrorMessage = string.Empty;
        try
        {
            await _userManager.DeleteUserAsync(user.Id).ConfigureAwait(true);
            Users.Remove(user);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除用户失败：{ex.Message}";
        }
    }
}
