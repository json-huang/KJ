namespace KJ.Domain;

public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3,
}

public enum TagQuality
{
    Good = 0,
    Bad = 1,
    Unknown = 2,
}

public readonly record struct TagId(string Value);

public readonly record struct TagValue(TagId Id, object? Value, TagQuality Quality, DateTimeOffset Timestamp);

public interface ITagStore
{
    event EventHandler<TagValue>? TagUpdated;
    bool TryGet(TagId id, out TagValue value);
    void Upsert(TagValue value);
}

public interface IDeviceManager
{
    IReadOnlyList<DeviceDescriptor> ListDevices();
}

public sealed record DeviceDescriptor(string DeviceId, string DisplayName, string DriverType);

public interface IAlarmService
{
    event EventHandler<AlarmEvent>? AlarmRaised;
    void Raise(AlarmEvent alarmEvent);
}

public sealed record AlarmEvent(string Code, string Message, AlarmSeverity Severity, DateTimeOffset Timestamp);

public interface IRecipeEngine
{
    Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default);
}

