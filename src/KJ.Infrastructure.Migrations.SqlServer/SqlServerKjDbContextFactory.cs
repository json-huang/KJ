using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KJ.Infrastructure.Migrations.SqlServer;

/// <summary>
/// Design-time factory for SQL Server migrations (avoids WinUI startup).
/// </summary>
public sealed class SqlServerKjDbContextFactory : IDesignTimeDbContextFactory<KjDbContext>
{
    public KjDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("KJ_CONNECTION_STRING")
            ?? "Server=localhost;Database=mesdb;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<KjDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(SqlServerKjDbContextFactory).Assembly.GetName().Name);
            })
            .Options;

        return new KjDbContext(options);
    }
}

