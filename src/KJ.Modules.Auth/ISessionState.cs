namespace KJ.Modules.Auth;

/// <summary>
/// 桌面端内存会话：登录成功后由 Shell 持有，供各 Prism 模块通过容器解析。
/// </summary>
public interface ISessionState
{
    bool IsSignedIn { get; }

    string? Email { get; }

    void SetSignedIn(string email);

    void Clear();
}
