using System.IO.Compression;
using System.Text;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 数据库备份服务。支持导出标签历史、告警历史、审计日志为 CSV，
/// 以及导出全部数据为 ZIP 压缩包。
/// </summary>
public sealed class DatabaseBackupService
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public DatabaseBackupService(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>导出指定时间范围的标签历史为 CSV。</summary>
    public async Task<byte[]> ExportTagHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var data = await db.TagHistory
            .Where(h => h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime)
            .OrderBy(h => h.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var tagNames = await db.Tags
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Id,TagId,TagName,Timestamp,Value,Quality");

        foreach (var row in data)
        {
            var tagName = tagNames.TryGetValue(row.TagId, out var name) ? name : row.TagId.ToString();
            sb.AppendLine($"{row.Id},{row.TagId},{EscapeCsv(tagName)},{row.Timestamp:O},{EscapeCsv(row.Value ?? "")},{row.Quality}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>导出指定时间范围的告警历史为 CSV。</summary>
    public async Task<byte[]> ExportAlarmHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var data = await db.AlarmHistory
            .Where(h => h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime)
            .OrderBy(h => h.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Id,AlarmId,Timestamp,EventType,Message,UserId");

        foreach (var row in data)
        {
            sb.AppendLine($"{row.Id},{row.AlarmId},{row.Timestamp:O},{EscapeCsv(row.EventType)},{EscapeCsv(row.Message ?? "")},{row.UserId ?? ""}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>导出指定时间范围的审计日志为 CSV。</summary>
    public async Task<byte[]> ExportAuditLogAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var data = await db.AuditLogs
            .Where(h => h.Timestamp >= from.UtcDateTime && h.Timestamp <= to.UtcDateTime)
            .OrderBy(h => h.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,UserId,Action,Details");

        foreach (var row in data)
        {
            sb.AppendLine($"{row.Id},{row.Timestamp:O},{row.UserId ?? ""},{EscapeCsv(row.Action)},{EscapeCsv(row.Details ?? "")}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>导出所有数据为 ZIP（包含三个 CSV 文件）。</summary>
    public async Task<byte[]> ExportAllAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var tagTask = ExportTagHistoryAsync(from, to, ct);
        var alarmTask = ExportAlarmHistoryAsync(from, to, ct);
        var auditTask = ExportAuditLogAsync(from, to, ct);

        await Task.WhenAll(tagTask, alarmTask, auditTask).ConfigureAwait(false);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteZipEntryAsync(archive, "TagHistory.csv", await tagTask.ConfigureAwait(false)).ConfigureAwait(false);
            await WriteZipEntryAsync(archive, "AlarmHistory.csv", await alarmTask.ConfigureAwait(false)).ConfigureAwait(false);
            await WriteZipEntryAsync(archive, "AuditLog.csv", await auditTask.ConfigureAwait(false)).ConfigureAwait(false);
        }

        return memoryStream.ToArray();
    }

    private static async Task WriteZipEntryAsync(ZipArchive archive, string entryName, byte[] data)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(data).ConfigureAwait(false);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
