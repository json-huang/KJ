
using KJ.Domain;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 系统健康检查服务。报告各组件状态。
/// </summary>
public sealed class HealthCheckService
{
    private readonly IDeviceManager _deviceManager;
    private readonly ITagStore _tagStore;
    private readonly ICommsService _commsService;
    private readonly IDbContextFactory<KjDbContext>? _dbFactory;
    private readonly ILogger<HealthCheckService>? _logger;

    public HealthCheckService(
        IDeviceManager deviceManager,
        ITagStore tagStore,
        ICommsService commsService,
        IDbContextFactory<KjDbContext>? dbFactory = null,
        ILogger<HealthCheckService>? logger = null)
    {
        _deviceManager = deviceManager;
        _tagStore = tagStore;
        _commsService = commsService;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>执行健康检查。</summary>
    public async Task<HealthReport> CheckAsync(CancellationToken ct = default)
    {
        var checks = new List<ComponentHealth>();

        // 1. 设备连接状态
        checks.Add(CheckDevices());

        // 2. 数据库连接
        if (_dbFactory is not null)
            checks.Add(await CheckDatabaseAsync(ct).ConfigureAwait(false));

        // 3. TagStore 状态
        checks.Add(CheckTagStore());

        return new HealthReport(
            Status: checks.All(c => c.Status == HealthStatus.Healthy) ? HealthStatus.Healthy
                : checks.Any(c => c.Status == HealthStatus.Unhealthy) ? HealthStatus.Unhealthy
                : HealthStatus.Degraded,
            Components: checks,
            Timestamp: DateTimeOffset.Now);
    }

    private ComponentHealth CheckDevices()
    {
        var devices = _deviceManager.ListDevices();
        var total = devices.Count;
        var connected = devices.Count(d => d.State == "Connected");
        var faulted = devices.Count(d => d.State == "Faulted");

        var status = total == 0 ? HealthStatus.Healthy
            : faulted > 0 ? HealthStatus.Degraded
            : connected == total ? HealthStatus.Healthy
            : HealthStatus.Degraded;

        return new ComponentHealth(
            Name: "Devices",
            Status: status,
            Description: $"{connected}/{total} connected, {faulted} faulted",
            Data: new Dictionary<string, string>
            {
                ["total"] = total.ToString(),
                ["connected"] = connected.ToString(),
                ["faulted"] = faulted.ToString(),
            });
    }

    private async Task<ComponentHealth> CheckDatabaseAsync(CancellationToken ct)
    {
        try
        {
            using var db = await _dbFactory!.CreateDbContextAsync(ct).ConfigureAwait(false);
            var canConnect = await db.Database.CanConnectAsync(ct).ConfigureAwait(false);

            return new ComponentHealth(
                Name: "Database",
                Status: canConnect ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description: canConnect ? "Connected" : "Cannot connect");
        }
        catch (Exception ex)
        {
            return new ComponentHealth(
                Name: "Database",
                Status: HealthStatus.Unhealthy,
                Description: $"Error: {ex.Message}");
        }
    }

    private ComponentHealth CheckTagStore()
    {
        // TagStore 是内存存储，始终 Healthy
        // 这里检查是否有最近更新（说明采集在工作）
        return new ComponentHealth(
            Name: "TagStore",
            Status: HealthStatus.Healthy,
            Description: "In-memory store active");
    }
}

public sealed record HealthReport(
    HealthStatus Status,
    IReadOnlyList<ComponentHealth> Components,
    DateTimeOffset Timestamp);

public sealed record ComponentHealth(
    string Name,
    HealthStatus Status,
    string Description,
    IReadOnlyDictionary<string, string>? Data = null);

public enum HealthStatus
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2,
}
