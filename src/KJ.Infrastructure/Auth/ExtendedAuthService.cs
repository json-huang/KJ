using KJ.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Auth;

/// <summary>
/// 扩展认证服务。支持密码重置、用户注册。
/// </summary>
public sealed class ExtendedAuthService
{
    private readonly IUserManager _userManager;
    private readonly IRoleManager _roleManager;
    private readonly ILogger<ExtendedAuthService>? _logger;

    public ExtendedAuthService(
        IUserManager userManager,
        IRoleManager roleManager,
        ILogger<ExtendedAuthService>? logger = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>注册新用户。</summary>
    public async Task<(bool Success, string? Error)> RegisterAsync(
        string name, string email, string password, string? role = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "请填写所有字段。");

        if (password.Length < 8)
            return (false, "密码长度至少 8 位。");

        try
        {
            var user = new AppUser(string.Empty, name.Trim(), email.Trim());
            await _userManager.CreateUserAsync(user, password, ct).ConfigureAwait(false);

            _logger?.LogInformation("User registered: {Email}", email);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Registration failed for {Email}", email);
            return (false, $"注册失败：{ex.Message}");
        }
    }

    /// <summary>重置密码（管理员操作）。</summary>
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string userId, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "密码不能为空。");

        if (newPassword.Length < 8)
            return (false, "密码长度至少 8 位。");

        try
        {
            // 管理员重置：用空旧密码调用（需要实现层处理）
            await _userManager.ChangePasswordAsync(userId, "", newPassword, ct).ConfigureAwait(false);
            _logger?.LogInformation("Password reset for user {UserId}", userId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Password reset failed for {UserId}", userId);
            return (false, $"密码重置失败：{ex.Message}");
        }
    }

    /// <summary>修改自己的密码。</summary>
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        string userId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "新密码不能为空。");

        if (newPassword.Length < 8)
            return (false, "密码长度至少 8 位。");

        if (oldPassword == newPassword)
            return (false, "新密码不能和旧密码相同。");

        try
        {
            await _userManager.ChangePasswordAsync(userId, oldPassword, newPassword, ct).ConfigureAwait(false);
            _logger?.LogInformation("Password changed for user {UserId}", userId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Password change failed for {UserId}", userId);
            return (false, $"密码修改失败：{ex.Message}");
        }
    }

    /// <summary>获取用户列表。</summary>
    public async Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken ct = default)
    {
        return await _userManager.GetUsersAsync(ct).ConfigureAwait(false);
    }

    /// <summary>删除用户。</summary>
    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        await _userManager.DeleteUserAsync(userId, ct).ConfigureAwait(false);
        _logger?.LogInformation("User deleted: {UserId}", userId);
    }
}
