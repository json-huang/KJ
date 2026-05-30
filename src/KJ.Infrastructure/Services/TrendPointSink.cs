using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 兼容两种方案的趋势点写入：
/// 1) 优先写入 TagStore（触发 TagHistoryWriter 管道）
/// 2) 同时直接写入 TagHistory，保证“立即可查/立即出图”
/// </summary>
public sealed class TrendPointSink : KJ.Workflows.ITrendPointSink
{
    private readonly ITagStore _tagStore;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public TrendPointSink(ITagStore tagStore, IDbContextFactory<KjDbContext> dbFactory)
    {
        _tagStore = tagStore;
        _dbFactory = dbFactory;
    }

    public async Task WriteAsync(string tagKey, object? value, DateTimeOffset? timestamp = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tagKey))
            return;

        var ts = timestamp ?? DateTimeOffset.Now;

        // 方案B：TagStore（让系统其他地方也能看到当前值）
        _tagStore.Upsert(new TagValue(new TagId(tagKey), value, TagQuality.Good, ts));

        // 方案A：直接落 TagHistory（立即能在趋势图查询到）
        var tagId = TagIdentity.GetTagId(tagKey);
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        // 确保 Tags 表存在该 tag，否则趋势查询会漏
        if (!await db.Tags.AnyAsync(t => t.Id == tagId, ct).ConfigureAwait(false))
        {
            db.Tags.Add(new Tag
            {
                Id = tagId,
                DeviceId = TagIdentity.SimulatedDeviceId,
                Name = tagKey,
                DataType = TagDataType.String,
                Address = tagKey,
                Quality = QualityCode.Good,
                Timestamp = ts.UtcDateTime,
                Direction = TagDirection.Read,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        db.TagHistory.Add(new TagHistory
        {
            Id = Guid.NewGuid(),
            TagId = tagId,
            Timestamp = ts.UtcDateTime,
            Value = value?.ToString(),
            Quality = QualityCode.Good,
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

