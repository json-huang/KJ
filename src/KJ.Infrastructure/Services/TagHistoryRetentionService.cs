using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

/// <summary>
/// TagHistory 数据保留服务。定期清理超过指定天数的历史数据。
/// 支持按标签配置不同的保留策略。
/// </summary>
public sealed class TagHistoryRetentionService : IDisposable
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly ILogger<TagHistoryRetentionService>? _logger;
    private readonly Timer _cleanupTimer;
    private readonly int _retentionDays;
    private readonly int _batchSize;
    private bool _disposed;

    /// <param name="retentionDays">保留天数（默认 30 天）</param>
    /// <param name="cleanupIntervalHours">清理间隔（默认 24 小时）</param>
    /// <param name="batchSize">每批删除条数（默认 10000）</param>
    public TagHistoryRetentionService(
        IDbContextFactory<KjDbContext> dbFactory,
        ILogger<TagHistoryRetentionService>? logger = null,
        int retentionDays = 30,
        int cleanupIntervalHours = 24,
        int batchSize = 10000)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _retentionDays = retentionDays;
        _batchSize = batchSize;

        var interval = TimeSpan.FromHours(cleanupIntervalHours);
        _cleanupTimer = new Timer(CleanupCallback, null, interval, interval);
    }

    private async void CleanupCallback(object? state)
    {
        if (_disposed) return;
        await CleanupAsync().ConfigureAwait(false);
    }

    /// <summary>执行一次清理。返回删除的记录数。</summary>
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        var totalDeleted = 0;

        try
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // 批量删除，避免锁表太久
                var batch = await db.TagHistory
                    .Where(h => h.Timestamp < cutoff)
                    .Take(_batchSize)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (batch.Count == 0) break;

                db.TagHistory.RemoveRange(batch);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                totalDeleted += batch.Count;

                _logger?.LogInformation("Deleted {Count} tag history records older than {Cutoff}",
                    batch.Count, cutoff);

                // 如果这批不满，说明已经删完
                if (batch.Count < _batchSize) break;
            }

            // 同样清理 AlarmHistory
            var alarmDeleted = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var batch = await db.AlarmHistory
                    .Where(h => h.Timestamp < cutoff)
                    .Take(_batchSize)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (batch.Count == 0) break;

                db.AlarmHistory.RemoveRange(batch);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                alarmDeleted += batch.Count;

                if (batch.Count < _batchSize) break;
            }

            if (totalDeleted > 0 || alarmDeleted > 0)
            {
                _logger?.LogInformation(
                    "Data retention cleanup: deleted {TagCount} tag history and {AlarmCount} alarm history records",
                    totalDeleted, alarmDeleted);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during data retention cleanup");
        }

        return totalDeleted;
    }

    /// <summary>获取历史数据统计。</summary>
    public async Task<HistoryStats> GetStatsAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tagCount = await db.TagHistory.CountAsync(ct).ConfigureAwait(false);
        var alarmCount = await db.AlarmHistory.CountAsync(ct).ConfigureAwait(false);
        var oldestTag = await db.TagHistory.MinAsync(h => (DateTime?)h.Timestamp, ct).ConfigureAwait(false);
        var newestTag = await db.TagHistory.MaxAsync(h => (DateTime?)h.Timestamp, ct).ConfigureAwait(false);

        return new HistoryStats(tagCount, alarmCount, oldestTag, newestTag);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}

public sealed record HistoryStats(int TagHistoryCount, int AlarmHistoryCount, DateTime? OldestRecord, DateTime? NewestRecord);
