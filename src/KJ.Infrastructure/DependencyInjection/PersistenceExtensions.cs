using KJ.Infrastructure.Auth;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Identity;
using KJ.Infrastructure.Logging;
using KJ.Domain;
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

        void ConfigureDb(DbContextOptionsBuilder options)
        {
            var provider = (configuration["Database:Provider"] ?? "MySql").Trim();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Mssql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("KJ.Infrastructure.Migrations.SqlServer");
                });
                return;
            }

            var serverVersion = ServerVersion.AutoDetect(connectionString);
            options.UseMySql(connectionString, serverVersion, mySql =>
            {
                mySql.MigrationsAssembly(typeof(KjDbContext).Assembly.GetName().Name);
            });
        }

        services.AddDbContext<KjDbContext>(ConfigureDb);
        services.AddDbContextFactory<KjDbContext>(ConfigureDb);

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
        services.AddSingleton<DatabaseInitSignal>();
        services.AddSingleton<IDatabaseInitSignal>(sp => sp.GetRequiredService<DatabaseInitSignal>());
        services.AddSingleton<IDashboardDemoDataEnsurer, DashboardDemoDataEnsurer>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<IUserManager, IdentityUserManager>();
        services.AddScoped<IRoleManager, IdentityRoleManager>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        return services;
    }
}
