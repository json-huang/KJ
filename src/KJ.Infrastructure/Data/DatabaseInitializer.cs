using System.Reflection;
using System.Security.Claims;
using KJ.Domain.Security;
using KJ.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Data;

public sealed class DatabaseInitializer
{
    private readonly KjDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IDashboardDemoDataEnsurer _dashboardDemoDataEnsurer;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        KjDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IDashboardDemoDataEnsurer dashboardDemoDataEnsurer,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _dashboardDemoDataEnsurer = dashboardDemoDataEnsurer;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        const string adminRole = "Admin";
        if (!await _roleManager.RoleExistsAsync(adminRole).ConfigureAwait(false))
        {
            var created = await _roleManager.CreateAsync(new IdentityRole(adminRole)).ConfigureAwait(false);
            if (!created.Succeeded)
                _logger.LogWarning("Failed to create role {Role}: {Errors}", adminRole, string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        await EnsureAdminRolePermissionsAsync(adminRole).ConfigureAwait(false);

        var email = _configuration["Seed:AdminEmail"] ?? "admin@local";
        var password = _configuration["Seed:AdminPassword"] ?? "Admin123!";

        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };

            var result = await _userManager.CreateAsync(user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to seed admin user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            else
            {
                await _userManager.AddToRoleAsync(user, adminRole).ConfigureAwait(false);
                _logger.LogInformation("Seeded admin user {Email} with role {Role}.", email, adminRole);
            }
        }

        await EnsureSeedDeviceAndTagAsync(cancellationToken).ConfigureAwait(false);
        await _dashboardDemoDataEnsurer.EnsureAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureSeedDeviceAndTagAsync(CancellationToken cancellationToken)
    {
        if (!await _db.Devices.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            _db.Devices.Add(new Device
            {
                Id = TagIdentity.SimulatedDeviceId,
                Name = "Simulated",
                Description = "Local development seed device.",
                Type = DeviceType.Plc,
                State = ConnectionState.Connected,
                LastConnected = DateTime.Now,
                Address = new DeviceAddress { Host = "127.0.0.1", Port = 0 },
                PropertiesJson = "{}",
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var heartbeatTagId = TagIdentity.GetTagId("Heartbeat");
        if (!await _db.Tags.AnyAsync(t => t.Id == heartbeatTagId, cancellationToken).ConfigureAwait(false))
        {
            _db.Tags.Add(new Tag
            {
                Id = heartbeatTagId,
                DeviceId = TagIdentity.SimulatedDeviceId,
                Name = "Heartbeat",
                DataType = TagDataType.String,
                Address = "Heartbeat",
                Quality = QualityCode.Uncertain,
                Timestamp = DateTime.Now,
                Direction = TagDirection.Read,
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureAdminRolePermissionsAsync(string adminRoleName)
    {
        var role = await _roleManager.FindByNameAsync(adminRoleName).ConfigureAwait(false);
        if (role is null)
            return;

        var existing = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        var existingSet = existing
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permissionValues = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var p in permissionValues)
        {
            if (existingSet.Contains(p))
                continue;

            var result = await _roleManager.AddClaimAsync(role, new Claim("permission", p)).ConfigureAwait(false);
            if (!result.Succeeded)
                _logger.LogWarning("Failed to grant permission {Permission} to role {Role}: {Errors}", p, adminRoleName, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
