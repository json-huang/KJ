namespace KJ.Domain;

public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3,
}

public enum AlarmCondition
{
    GreaterThan = 0,
    LessThan = 1,
    Equals = 2,
    NotEquals = 3,
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

// ── Device ──────────────────────────────────────────────────────────────

public sealed record DeviceDescriptor(string DeviceId, string DisplayName, string DriverType, string State = "Disconnected");

public interface IDeviceManager
{
    IReadOnlyList<DeviceDescriptor> ListDevices();
    DeviceDescriptor? GetDevice(string deviceId);
    void AddDevice(DeviceDescriptor device);
    void RemoveDevice(string deviceId);
    void UpdateDeviceState(string deviceId, string state);
}

// ── Alarm ───────────────────────────────────────────────────────────────

public sealed record AlarmEvent(string Code, string Message, AlarmSeverity Severity, DateTimeOffset Timestamp);

public sealed record AlarmRule(
    string Id,
    string TagKey,
    AlarmCondition Condition,
    AlarmSeverity Severity,
    string Message,
    bool IsEnabled);

public sealed record ActiveAlarm(
    string Id,
    string RuleId,
    string TagKey,
    string Message,
    AlarmSeverity Severity,
    DateTimeOffset TriggeredAt,
    bool Acknowledged,
    string? AcknowledgedBy);

public interface IAlarmService
{
    event EventHandler<AlarmEvent>? AlarmRaised;
    void Raise(AlarmEvent alarmEvent);
    void AddRule(AlarmRule rule);
    void RemoveRule(string ruleId);
    IReadOnlyList<AlarmRule> GetRules();
    IReadOnlyList<ActiveAlarm> GetActiveAlarms();
    void AcknowledgeAlarm(string alarmId, string userId);
    void ClearAlarm(string alarmId);
    void Evaluate(string tagKey, object? value);
}

// ── Recipe ──────────────────────────────────────────────────────────────

public sealed record RecipeData(
    string Name,
    string Version,
    IReadOnlyList<RecipeParameterData> Parameters,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record RecipeParameterData(string Key, string Value);

public interface IRecipeEngine
{
    Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default);
    Task<RecipeData?> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeData>> GetRecipesAsync(CancellationToken cancellationToken = default);
    Task SaveRecipeAsync(RecipeData recipe, CancellationToken cancellationToken = default);
    Task DeleteRecipeAsync(string recipeName, CancellationToken cancellationToken = default);
}

// ── Audit ───────────────────────────────────────────────────────────────

public sealed record AuditEntry(string UserId, string Action, string? Details, DateTimeOffset Timestamp);

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetLogsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
