using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 数据保留策略管理器。支持为不同数据类型配置独立的保留天数，
/// 手动触发清理，并返回清理统计信息。
/// </summary>
public sealed class DataRetentionManager
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly ILogger<DataRetentionManager>? _logger;
    private readonly Dictionary<string, RetentionPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public DataRetentionManager(
        IDbContextFactory<KjDbContext> dbFactory,
        ILogger<DataRetentionManager>? logger = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;

        // 默认策略
        AddPolicy(new RetentionPolicy("TagHistory", 30));
        AddPolicy(new RetentionPolicy("AlarmHistory", 90));
        AddPolicy(new RetentionPolicy("AuditLog", 180));
    }

    /// <summary>添加或更新保留策略。</summary>
    public void AddPolicy(RetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_lock)
        {
            _policies[policy.Name] = policy;
        }
        _logger?.LogInformation("Retention policy added/updated: {Name} -> {Days} days",
            policy.Name, policy.RetentionDays);
    }

    /// <summary>获取所有保留策略。</summary>
    public IReadOnlyList<RetentionPolicy> GetPolicies()
    {
        lock (_lock)
        {
            return _policies.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>按名称获取策略。未找到返回 null。</summary>
    public RetentionPolicy? GetPolicy(string name)
    {
        lock (_lock)
        {
            return _policies.TryGetValue(name, out var p) ? p : null;
        }
    }

    /// <summary>移除策略。</summary>
    public bool RemovePolicy(string name)
    {
        lock (_lock)
        {
            return _policies.Remove(name);
        }
    }

    /// <summary>手动触发所有策略的清理。返回每个策略的清理统计。</summary>
    public async Task<IReadOnlyList<CleanupResult>> CleanupAllAsync(CancellationToken ct = default)
    {
        List<RetentionPolicy> snapshot;
        lock (_lock)
        {
            snapshot = _policies.Values.ToList();
        }

        var results = new List<CleanupResult>(snapshot.Count);
        foreach (var policy in snapshot)
        {
            var result = await CleanupAsync(policy, ct).ConfigureAwait(false);
            results.Add(result);
        }
        return results.AsReadOnly();
    }

    /// <summary>触发指定策略的清理。</summary>
    public async Task<CleanupResult> CleanupAsync(string policyName, CancellationToken ct = default)
    {
        RetentionPolicy? policy;
        lock (_lock)
        {
            _policies.TryGetValue(policyName, out policy);
        }

        if (policy is null)
            throw new ArgumentException($"Retention policy '{policyName}' not found.", nameof(policyName));

        return await CleanupAsync(policy, ct).ConfigureAwait(false);
    }

    private async Task<CleanupResult> CleanupAsync(RetentionPolicy policy, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
        var deletedCount = 0;

        try
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            switch (policy.Name)
            {
                case "TagHistory":
                    deletedCount = await BatchDeleteAsync(
                        db.TagHistory.Where(h => h.Timestamp < cutoff),
                        db, ct).ConfigureAwait(false);
                    break;

                case "AlarmHistory":
                    deletedCount = await BatchDeleteAsync(
                        db.AlarmHistory.Where(h => h.Timestamp < cutoff),
                        db, ct).ConfigureAwait(false);
                    break;

                case "AuditLog":
                    deletedCount = await BatchDeleteAsync(
                        db.AuditLogs.Where(h => h.Timestamp < cutoff),
                        db, ct).ConfigureAwait(false);
                    break;

                default:
                    _logger?.LogWarning("Unknown retention policy: {Name}", policy.Name);
                    return new CleanupResult(policy.Name, 0, cutoff, "Unknown policy type");
            }

            _logger?.LogInformation(
                "Cleanup completed for {Name}: deleted {Count} records older than {Cutoff}",
                policy.Name, deletedCount, cutoff);

            return new CleanupResult(policy.Name, deletedCount, cutoff, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cleanup for policy {Name}", policy.Name);
            return new CleanupResult(policy.Name, 0, cutoff, ex.Message);
        }
    }

    private static async Task<int> BatchDeleteAsync<T>(
        IQueryable<T> query,
        KjDbContext db,
        CancellationToken ct,
        int batchSize = 10000) where T : class
    {
        var totalDeleted = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await query.Take(batchSize).ToListAsync(ct).ConfigureAwait(false);
            if (batch.Count == 0) break;

            db.Set<T>().RemoveRange(batch);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            totalDeleted += batch.Count;

            if (batch.Count < batchSize) break;
        }

        return totalDeleted;
    }
}

/// <summary>数据保留策略。</summary>
public sealed class RetentionPolicy
{
    public string Name { get; }
    public int RetentionDays { get; set; }

    public RetentionPolicy(string name, int retentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionDays);

        Name = name;
        RetentionDays = retentionDays;
    }
}

/// <summary>清理结果。</summary>
public sealed record CleanupResult(
    string PolicyName,
    int DeletedCount,
    DateTime Cutoff,
    string? Error);
