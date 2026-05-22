using System.Collections.ObjectModel;
using KJ.Domain.Identity;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Auth.ViewModels;

public sealed class RoleManagementViewModel : BindableBase
{
    private readonly IRoleManager _roleManager;

    public ObservableCollection<AppRole> Roles { get; } = new();

    private string _newRoleName = string.Empty;
    public string NewRoleName
    {
        get => _newRoleName;
        set => SetProperty(ref _newRoleName, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public DelegateCommand LoadRolesCommand { get; }
    public DelegateCommand CreateRoleCommand { get; }
    public DelegateCommand<AppRole> DeleteRoleCommand { get; }

    public RoleManagementViewModel(IRoleManager roleManager)
    {
        _roleManager = roleManager;
        LoadRolesCommand = new DelegateCommand(() => _ = ExecuteLoadRolesAsync());
        CreateRoleCommand = new DelegateCommand(() => _ = ExecuteCreateRoleAsync());
        DeleteRoleCommand = new DelegateCommand<AppRole>(r => { if (r is not null) _ = ExecuteDeleteRoleAsync(r); });

        _ = ExecuteLoadRolesAsync();
    }

    private async Task ExecuteLoadRolesAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var roles = await _roleManager.GetRolesAsync().ConfigureAwait(true);
            Roles.Clear();
            foreach (var r in roles)
                Roles.Add(r);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载角色失败：{ex.Message}";
        }
    }

    private async Task ExecuteCreateRoleAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewRoleName))
        {
            ErrorMessage = "请输入角色名称。";
            return;
        }

        try
        {
            var role = new AppRole(string.Empty, NewRoleName.Trim());
            await _roleManager.CreateRoleAsync(role).ConfigureAwait(true);
            NewRoleName = string.Empty;
            await ExecuteLoadRolesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"创建角色失败：{ex.Message}";
        }
    }

    private async Task ExecuteDeleteRoleAsync(AppRole role)
    {
        ErrorMessage = string.Empty;
        try
        {
            await _roleManager.DeleteRoleAsync(role.Id).ConfigureAwait(true);
            Roles.Remove(role);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除角色失败：{ex.Message}";
        }
    }
}
