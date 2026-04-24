namespace KJ.App.Services;

/// <summary>
/// 启动时尝试用本机已保存的凭据静默登录。
/// </summary>
public interface ISessionResumeService
{
    Task<bool> TryResumeAsync(CancellationToken cancellationToken = default);
}
