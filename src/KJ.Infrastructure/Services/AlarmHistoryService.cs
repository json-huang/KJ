using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 告警历史查询服务。支持按时间、严重程度、标签过滤。
/// </summary>
public sealed class AlarmHistoryService
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public AlarmHistoryService(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>查询告警历史。</summary>
    public async Task<IReadOnlyList<AlarmHistoryEntry>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        AlarmSeverity? minSeverity = null,
        string? tagKey = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var query = db.AlarmHistory
            .Where(h => h.EventType == "Triggered");

        if (from.HasValue)
            query = query.Where(h => h.Timestamp >= from.Value.UtcDateTime);
        if (to.HasValue)
            query = query.Where(h => h.Timestamp <= to.Value.UtcDateTime);
        if (!string.IsNullOrWhiteSpace(tagKey))
        {
            var tagIds = db.Tags.Where(t => t.Name.Contains(tagKey)).Select(t => t.Id);
            var alarmIds = db.Alarms.Where(a => tagIds.Contains(a.TagId)).Select(a => a.Id);
            query = query.Where(h => alarmIds.Contains(h.AlarmId));
        }

        var entries = await query
            .OrderByDescending(h => h.Timestamp)
            .Take(limit)
            .Select(h => new AlarmHistoryEntry
            {
                Id = h.Id,
                AlarmId = h.AlarmId,
                Timestamp = h.Timestamp,
                Message = h.Message ?? "",
                EventType = h.EventType,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entries.AsReadOnly();
    }

    /// <summary>获取告警统计。</summary>
    public async Task<AlarmStats> GetStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var triggered = await db.AlarmHistory
            .CountAsync(h => h.EventType == "Triggered" && h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime, ct);

        var acknowledged = await db.AlarmHistory
            .CountAsync(h => h.EventType == "Acknowledged" && h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime, ct);

        return new AlarmStats(triggered, acknowledged, from, to);
    }

    /// <summary>导出告警历史为 CSV。</summary>
    public async Task<byte[]> ExportToCsvAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var entries = await QueryAsync(from, to, ct: ct).ConfigureAwait(false);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Timestamp,AlarmId,Message,EventType");

        foreach (var entry in entries)
        {
            sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.AlarmId},{EscapeCsv(entry.Message)},{entry.EventType}");
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

public sealed class AlarmHistoryEntry
{
    public Guid Id { get; set; }
    public Guid AlarmId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public string EventType { get; set; } = "";
}

public sealed record AlarmStats(int Triggered, int Acknowledged, DateTimeOffset From, DateTimeOffset To);
