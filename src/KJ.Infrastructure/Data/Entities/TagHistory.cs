namespace KJ.Infrastructure.Data.Entities;

public sealed class TagHistory
{
    public Guid Id { get; set; }

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string? Value { get; set; }

    public QualityCode Quality { get; set; }
}
