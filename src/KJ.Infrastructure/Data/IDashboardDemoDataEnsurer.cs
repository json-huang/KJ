namespace KJ.Infrastructure.Data;

/// <summary>
/// 确保 Dashboard 有可展示的演示数据（内存 + 数据库）。
/// </summary>
public interface IDashboardDemoDataEnsurer
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}
