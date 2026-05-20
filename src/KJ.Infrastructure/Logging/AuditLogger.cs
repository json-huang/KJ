using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Logging;

public sealed class AuditLogger : IAuditLogger
{
    private readonly KjDbContext _db;

    public AuditLogger(KjDbContext db) => _db = db;

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = entry.Timestamp.UtcDateTime,
            UserId = entry.UserId,
            Action = entry.Action,
            Details = entry.Details,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetLogsAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        return await _db.AuditLogs
            .Where(l => l.Timestamp >= start.UtcDateTime && l.Timestamp <= end.UtcDateTime)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditEntry(l.UserId ?? string.Empty, l.Action, l.Details, new DateTimeOffset(l.Timestamp, TimeSpan.Zero)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
