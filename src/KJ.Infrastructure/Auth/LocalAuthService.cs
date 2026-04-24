using Microsoft.AspNetCore.Identity;

namespace KJ.Infrastructure.Auth;

public sealed class LocalAuthService : ILocalAuthService
{
    private readonly UserManager<IdentityUser> _userManager;

    public LocalAuthService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(bool Success, string? ErrorMessage)> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "请输入邮箱和密码。");

        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
            return (false, "用户不存在。");

        var ok = await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false);
        return ok ? (true, null) : (false, "密码错误。");
    }
}
