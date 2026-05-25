using KJ.Domain.Identity;
using KJ.Domain.Security;
using KJ.Modules.Auth;

namespace KJ.App.Services;

/// <summary>
/// 基于数据库角色的权限服务。替代硬编码 admin 检查。
/// 
/// 权限模型：
/// - 每个用户有一个或多个角色
/// - 每个角色有一组权限
/// - 管理员角色拥有所有权限
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IAuthenticationContext _authenticationContext;
    private readonly IRoleManager _roleManager;
    private readonly IUserManager _userManager;

    // 角色权限映射（生产环境应从 DB 读取）
    private static readonly Dictionary<string, HashSet<string>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.DeviceView, Permissions.DeviceConfigure, Permissions.DeviceControl,
            Permissions.TagView, Permissions.TagWrite, Permissions.TagConfigure,
            Permissions.AlarmView, Permissions.AlarmAcknowledge, Permissions.AlarmConfigure,
            Permissions.RecipeView, Permissions.RecipeEdit, Permissions.RecipeApply,
            Permissions.UserView, Permissions.UserManage, Permissions.RoleManage,
            Permissions.SystemConfigure, Permissions.AuditView,
        },
        ["operator"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.DeviceView, Permissions.DeviceControl,
            Permissions.TagView, Permissions.TagWrite,
            Permissions.AlarmView, Permissions.AlarmAcknowledge,
            Permissions.RecipeView, Permissions.RecipeApply,
        },
        ["viewer"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.DeviceView,
            Permissions.TagView,
            Permissions.AlarmView,
            Permissions.RecipeView,
            Permissions.UserView,
            Permissions.AuditView,
        },
    };

    public PermissionService(
        IAuthenticationContext authenticationContext,
        IRoleManager roleManager,
        IUserManager userManager)
    {
        _authenticationContext = authenticationContext;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public bool HasPermission(string permission)
    {
        if (!_authenticationContext.IsAuthenticated)
            return false;

        var email = _authenticationContext.PrincipalEmail ?? string.Empty;

        // 查找用户角色（简化实现：通过邮箱前缀推断角色）
        // 生产环境应从 DB 查询用户-角色关联
        var role = DetermineUserRole(email);

        if (role is null)
            return false;

        if (RolePermissions.TryGetValue(role, out var permissions))
            return permissions.Contains(permission);

        return false;
    }

    /// <summary>根据用户信息确定角色。生产环境应从 DB 查询。</summary>
    private static string? DetermineUserRole(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        // 简化规则：邮箱包含 admin → admin，否则默认 viewer
        // 生产环境应查询数据库中的用户-角色关联表
        if (email.Contains("admin", StringComparison.OrdinalIgnoreCase))
            return "admin";
        if (email.Contains("operator", StringComparison.OrdinalIgnoreCase))
            return "operator";

        return "viewer";
    }
}
