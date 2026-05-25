using KJ.Domain;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 操作审计查询服务。查询用户操作日志。
/// </summary>
public sealed class AuditQueryService
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public AuditQueryService(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>查询审计日志。</summary>
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? userId = null,
        string? action = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var query = db.AuditLogs.AsQueryable();

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value.UtcDateTime);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value.UtcDateTime);
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(l => l.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action.Contains(action));

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .Select(l => new AuditLogEntry
            {
                Id = l.Id,
                UserId = l.UserId,
                Action = l.Action,
                Details = l.Details,
                Timestamp = l.Timestamp,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>获取操作统计。</summary>
    public async Task<AuditStats> GetStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var total = await db.AuditLogs
            .CountAsync(l => l.Timestamp >= from.UtcDateTime && l.Timestamp <= to.UtcDateTime, ct);

        var byAction = await db.AuditLogs
            .Where(l => l.Timestamp >= from.UtcDateTime && l.Timestamp <= to.UtcDateTime)
            .GroupBy(l => l.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToDictionaryAsync(x => x.Action, x => x.Count, ct);

        var byUser = await db.AuditLogs
            .Where(l => l.Timestamp >= from.UtcDateTime && l.Timestamp <= to.UtcDateTime)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        return new AuditStats(total, byAction, byUser, from, to);
    }
}

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed record AuditStats(
    int TotalOperations,
    IReadOnlyDictionary<string, int> ByAction,
    IReadOnlyDictionary<string, int> ByUser,
    DateTimeOffset From,
    DateTimeOffset To);
