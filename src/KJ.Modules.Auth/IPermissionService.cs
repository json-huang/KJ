namespace KJ.Modules.Auth;

/// <summary>
/// 基于当前登录会话的权限判定（后续可接 Identity 角色/声明）。
/// </summary>
public interface IPermissionService
{
    bool HasPermission(string permission);
}
