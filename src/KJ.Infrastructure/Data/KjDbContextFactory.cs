using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KJ.Infrastructure.Data;

/// <summary>
/// 设计时工厂：供 <c>dotnet ef</c> 生成迁移使用（不依赖 WinUI 启动路径）。
/// </summary>
public sealed class KjDbContextFactory : IDesignTimeDbContextFactory<KjDbContext>
{
    public KjDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("KJ_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=mesdb;Uid=root;Pwd=root;SslMode=none;Charset=utf8mb4;";

        var options = new DbContextOptionsBuilder<KjDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new KjDbContext(options);
    }
}
