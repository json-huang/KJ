namespace KJ.Modules.Auth;

/// <summary>
/// 本机登录偏好：记住邮箱（明文存于 LocalSettings）与「保持登录」（DPAPI 保护后写入本地文件）。
/// 实现位于 KJ.App。
/// </summary>
public interface ILoginCredentialStore
{
    string? LoadRememberedEmail();

    void SaveRememberedEmail(string email);

    void ClearRememberedEmail();

    void SaveStaySignedIn(string email, string password);

    (string? Email, string? Password) TryLoadStaySignedIn();

    void ClearStaySignedIn();
}
