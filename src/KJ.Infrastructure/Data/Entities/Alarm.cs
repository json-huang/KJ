namespace KJ.Infrastructure.Data.Entities;

public sealed class Alarm
{
    public Guid Id { get; set; }

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public AlarmCondition Condition { get; set; }

    public AlarmLevel Level { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? TriggeredAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }

    public ICollection<AlarmHistory> History { get; set; } = new List<AlarmHistory>();
}
