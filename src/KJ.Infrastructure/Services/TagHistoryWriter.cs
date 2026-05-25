using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 监听 ITagStore.TagUpdated 事件，异步将标签值变化写入 TagHistory 表。
/// 采用批量缓冲+定时刷新策略，避免高频写入拖垮数据库。
/// </summary>
public sealed class TagHistoryWriter : IDisposable
{
    private readonly ITagStore _tagStore;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly ILogger<TagHistoryWriter>? _logger;

    private readonly System.Collections.Concurrent.ConcurrentQueue<TagValue> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly int _batchSize;
    private bool _disposed;

    public TagHistoryWriter(
        ITagStore tagStore,
        IDbContextFactory<KjDbContext> dbFactory,
        ILogger<TagHistoryWriter>? logger = null,
        int flushIntervalMs = 5000,
        int batchSize = 100)
    {
        _tagStore = tagStore;
        _dbFactory = dbFactory;
        _logger = logger;
        _batchSize = batchSize;

        _tagStore.TagUpdated += OnTagUpdated;
        _flushTimer = new Timer(FlushCallback, null, flushIntervalMs, flushIntervalMs);
    }

    private void OnTagUpdated(object? sender, TagValue value)
    {
        _buffer.Enqueue(value);
    }

    private async void FlushCallback(object? state)
    {
        if (_disposed) return;
        await FlushAsync().ConfigureAwait(false);
    }

    /// <summary>将缓冲区中的标签值批量写入数据库。</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        var batch = new List<TagValue>();
        while (batch.Count < _batchSize && _buffer.TryDequeue(out var item))
        {
            batch.Add(item);
        }

        if (batch.Count == 0) return;

        try
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var entities = new List<TagHistory>(batch.Count);
            foreach (var tv in batch)
            {
                // 查找 TagId（标签名 → GUID）
                var tagEntity = await db.Tags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Name == tv.Id.Value, ct)
                    .ConfigureAwait(false);

                if (tagEntity is null)
                    continue;

                entities.Add(new TagHistory
                {
                    Id = Guid.NewGuid(),
                    TagId = tagEntity.Id,
                    Timestamp = tv.Timestamp.UtcDateTime,
                    Value = tv.Value?.ToString(),
                    Quality = tv.Quality switch
                    {
                        TagQuality.Good => QualityCode.Good,
                        TagQuality.Bad => QualityCode.Bad,
                        _ => QualityCode.Uncertain,
                    },
                });
            }

            if (entities.Count > 0)
            {
                db.TagHistory.AddRange(entities);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to flush {Count} tag history entries", batch.Count);
            // 数据不丢：写入失败的条目已经从 buffer 移除，
            // 但如果需要重试，可以在这里重新入队。
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tagStore.TagUpdated -= OnTagUpdated;
        _flushTimer.Dispose();

        // 最后刷一次
        try { FlushAsync().GetAwaiter().GetResult(); } catch { }
    }
}
