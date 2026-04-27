namespace KJ.Infrastructure.Messaging;

public record TagValueChangedMessage(
    Guid TagId,
    string TagKey,
    object? Value,
    DateTimeOffset Timestamp,
    TagQualityDto Quality = TagQualityDto.Good);

public record AlarmTriggeredMessage(Guid AlarmId, Guid TagId, AlarmLevelDto Level, string Message);

public record DeviceStateChangedMessage(Guid DeviceId, ConnectionStateDto State);

public record RecipeAppliedMessage(Guid RecipeId, Guid DeviceId, string UserId);

public enum TagQualityDto
{
    Good = 0,
    Bad = 1,
    Unknown = 2,
}

public enum AlarmLevelDto
{
    Info = 0,
    Warning = 1,
    High = 2,
    Critical = 3,
}

public enum ConnectionStateDto
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Faulted = 3,
}
