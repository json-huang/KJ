namespace KJ.Infrastructure.Data.Entities;

public sealed class Device
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeviceType Type { get; set; }

    public ConnectionState State { get; set; }

    public DateTime LastConnected { get; set; }

    public DeviceAddress Address { get; set; } = new();

    /// <summary>设备扩展属性 JSON（文档中的 Dictionary 以 JSON 持久化）。</summary>
    public string PropertiesJson { get; set; } = "{}";

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public sealed class DeviceAddress
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }
}
