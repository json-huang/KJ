namespace KJ.Modules.Auth;

/// <summary>
/// 只读认证上下文，供 Auth 等模块页面在不需要引用主工程视图时使用。
/// </summary>
public interface IAuthenticationContext
{
    bool IsAuthenticated { get; }

    string? PrincipalEmail { get; }
}
