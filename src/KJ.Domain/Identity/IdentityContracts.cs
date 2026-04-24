namespace KJ.Domain.Identity;

public sealed record AppUser(string Id, string Username, string Email);

public sealed record AppRole(string Id, string Name);

public interface IUserManager
{
    Task<AppUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<AppUser?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<AppUser> CreateUserAsync(AppUser user, string password, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(AppUser user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> CheckPasswordAsync(AppUser user, string password, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default);
}

public interface IRoleManager
{
    Task<AppRole?> GetRoleAsync(string roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppRole>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<AppRole> CreateRoleAsync(AppRole role, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(AppRole role, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
    Task GrantPermissionAsync(string roleId, string permission, CancellationToken cancellationToken = default);
    Task RevokePermissionAsync(string roleId, string permission, CancellationToken cancellationToken = default);
}

public sealed record TokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);

public sealed record TokenValidationResult(bool IsValid, string? UserId, string? Error);

public interface ITokenService
{
    Task<TokenResult> GenerateTokenAsync(AppUser user, CancellationToken cancellationToken = default);
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsTokenRevokedAsync(string token, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<TokenResult> SignInAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);
}

