namespace KJ.Infrastructure.Data.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }
}
