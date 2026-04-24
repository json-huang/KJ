using KJ.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace KJ.Infrastructure.Identity;

public sealed class IdentityUserManager : IUserManager
{
    private readonly UserManager<IdentityUser> _userManager;

    public IdentityUserManager(UserManager<IdentityUser> userManager) => _userManager = userManager;

    public async Task<AppUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user is null ? null : ToAppUser(user);
    }

    public async Task<AppUser?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(username).ConfigureAwait(false);
        return user is null ? null : ToAppUser(user);
    }

    public Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Array.Empty<AppUser>());

    public async Task<AppUser> CreateUserAsync(AppUser user, string password, CancellationToken cancellationToken = default)
    {
        var identityUser = new IdentityUser
        {
            UserName = user.Username,
            Email = user.Email,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(identityUser, password).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        return ToAppUser(identityUser);
    }

    public async Task UpdateUserAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var identityUser = await _userManager.FindByIdAsync(user.Id).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User not found.");

        identityUser.UserName = user.Username;
        identityUser.Email = user.Email;
        var result = await _userManager.UpdateAsync(identityUser).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var identityUser = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (identityUser is null)
            return;

        var result = await _userManager.DeleteAsync(identityUser).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<bool> CheckPasswordAsync(AppUser user, string password, CancellationToken cancellationToken = default)
    {
        var identityUser = await _userManager.FindByIdAsync(user.Id).ConfigureAwait(false);
        if (identityUser is null)
            return false;
        return await _userManager.CheckPasswordAsync(identityUser, password).ConfigureAwait(false);
    }

    public async Task ChangePasswordAsync(string userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var identityUser = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User not found.");
        var result = await _userManager.ChangePasswordAsync(identityUser, oldPassword, newPassword).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private static AppUser ToAppUser(IdentityUser u) =>
        new(u.Id, u.UserName ?? u.Email ?? string.Empty, u.Email ?? string.Empty);
}

