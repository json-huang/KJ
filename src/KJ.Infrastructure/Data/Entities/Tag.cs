namespace KJ.Infrastructure.Data.Entities;

public sealed class Tag
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public TagDataType DataType { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? Value { get; set; }

    public QualityCode Quality { get; set; }

    public DateTime Timestamp { get; set; }

    public TagDirection Direction { get; set; }

    /// <summary>轮询间隔（毫秒）。0 表示使用默认值 1000ms。</summary>
    public int PollIntervalMs { get; set; }
}
