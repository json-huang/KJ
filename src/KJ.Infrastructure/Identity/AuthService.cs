using KJ.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace KJ.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<IdentityUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<TokenResult> SignInAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("请输入用户名/邮箱和密码。");

        var user = await _userManager.FindByEmailAsync(usernameOrEmail).ConfigureAwait(false)
            ?? await _userManager.FindByNameAsync(usernameOrEmail).ConfigureAwait(false);

        if (user is null)
            throw new InvalidOperationException("用户不存在。");

        var ok = await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false);
        if (!ok)
            throw new InvalidOperationException("密码错误。");

        var appUser = new AppUser(user.Id, user.UserName ?? user.Email ?? string.Empty, user.Email ?? string.Empty);
        return await _tokenService.GenerateTokenAsync(appUser, cancellationToken).ConfigureAwait(false);
    }
}

