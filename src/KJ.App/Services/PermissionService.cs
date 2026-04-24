using KJ.Domain.Security;
using KJ.Modules.Auth;

namespace KJ.App.Services;

/// <summary>
/// 桌面端占位策略：已登录且邮箱包含 admin 视为全权限；否则仅开放只读类权限。
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IAuthenticationContext _authenticationContext;

    public PermissionService(IAuthenticationContext authenticationContext) =>
        _authenticationContext = authenticationContext;

    public bool HasPermission(string permission)
    {
        if (!_authenticationContext.IsAuthenticated)
            return false;

        if (IsElevatedAdministrator())
            return true;

        return permission is Permissions.DeviceView or Permissions.TagView or Permissions.AlarmView
            or Permissions.RecipeView or Permissions.UserView or Permissions.AuditView;
    }

    private bool IsElevatedAdministrator()
    {
        var email = _authenticationContext.PrincipalEmail ?? string.Empty;
        return email.Contains("admin", StringComparison.OrdinalIgnoreCase);
    }
}
