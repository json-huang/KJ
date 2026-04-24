namespace KJ.Infrastructure.Data.Entities;

public sealed class AlarmHistory
{
    public Guid Id { get; set; }

    public Guid AlarmId { get; set; }

    public Alarm Alarm { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Message { get; set; }

    public string? UserId { get; set; }
}
