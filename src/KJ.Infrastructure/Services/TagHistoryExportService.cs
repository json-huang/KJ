using System.Text;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 标签历史数据导出服务。支持导出为 CSV 格式。
/// </summary>
public sealed class TagHistoryExportService
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public TagHistoryExportService(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// 导出指定时间范围内的标签历史为 CSV。
    /// </summary>
    public async Task<string> ExportToCsvAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? tagFilter = null,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var query = db.TagHistory
            .Where(h => h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime);

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            var tagIds = db.Tags
                .Where(t => t.Name.Contains(tagFilter))
                .Select(t => t.Id)
                .ToList();
            query = query.Where(h => tagIds.Contains(h.TagId));
        }

        var data = await query
            .OrderBy(h => h.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 获取标签名映射
        var tagNames = db.Tags.ToDictionary(t => t.Id, t => t.Name);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,TagName,Value,Quality");

        foreach (var row in data)
        {
            var tagName = tagNames.TryGetValue(row.TagId, out var name) ? name : row.TagId.ToString();
            var escapedValue = EscapeCsv(row.Value ?? "");
            sb.AppendLine($"{row.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{EscapeCsv(tagName)},{escapedValue},{row.Quality}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 导出为 CSV 字节数组（用于文件下载）。
    /// </summary>
    public async Task<byte[]> ExportToBytesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? tagFilter = null,
        CancellationToken ct = default)
    {
        var csv = await ExportToCsvAsync(from, to, tagFilter, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetBytes(csv);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
