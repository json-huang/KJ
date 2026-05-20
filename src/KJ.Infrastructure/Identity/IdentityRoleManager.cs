using System.Security.Claims;
using KJ.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace KJ.Infrastructure.Identity;

public sealed class IdentityRoleManager : IRoleManager
{
    private const string PermissionClaimType = "permission";

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;

    public IdentityRoleManager(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<AppRole?> GetRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
        return role is null ? null : new AppRole(role.Id, role.Name ?? string.Empty);
    }

    public Task<IReadOnlyList<AppRole>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = _roleManager.Roles.ToList();
        return Task.FromResult<IReadOnlyList<AppRole>>(
            roles.Select(r => new AppRole(r.Id, r.Name ?? string.Empty)).ToList().AsReadOnly());
    }

    public async Task<AppRole> CreateRoleAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        var identityRole = new IdentityRole(role.Name);
        var result = await _roleManager.CreateAsync(identityRole).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        return new AppRole(identityRole.Id, identityRole.Name ?? string.Empty);
    }

    public async Task UpdateRoleAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        var identityRole = await _roleManager.FindByIdAsync(role.Id).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Role not found.");
        identityRole.Name = role.Name;
        var result = await _roleManager.UpdateAsync(identityRole).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var identityRole = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
        if (identityRole is null)
            return;
        var result = await _roleManager.DeleteAsync(identityRole).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return false;

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            if (role is null)
                continue;

            var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            if (claims.Any(c => c.Type == PermissionClaimType && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    public async Task GrantPermissionAsync(string roleId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return;

        var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false)
            ?? await _roleManager.FindByNameAsync(roleId).ConfigureAwait(false);
        if (role is null)
            return;

        var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        if (claims.Any(c => c.Type == PermissionClaimType && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)))
            return;

        var result = await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, permission)).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task RevokePermissionAsync(string roleId, string permission, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return;

        var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false)
            ?? await _roleManager.FindByNameAsync(roleId).ConfigureAwait(false);
        if (role is null)
            return;

        var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        var toRemove = claims.Where(c => c.Type == PermissionClaimType && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var claim in toRemove)
        {
            var result = await _roleManager.RemoveClaimAsync(role, claim).ConfigureAwait(false);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}

