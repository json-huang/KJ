using KJ.Infrastructure.Auth;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Identity;
using KJ.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.Infrastructure.DependencyInjection;

public static class PersistenceExtensions
{
    public static IServiceCollection AddKjPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        services.AddDbContext<KjDbContext>(options => options.UseSqlServer(connectionString));

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                configuration.GetSection("Identity:Password").Bind(options.Password);
                if (options.Password.RequiredLength < 1)
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                }
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<KjDbContext>();

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<IUserManager, IdentityUserManager>();
        services.AddScoped<IRoleManager, IdentityRoleManager>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
