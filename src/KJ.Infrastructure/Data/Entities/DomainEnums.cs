namespace KJ.Infrastructure.Data.Entities;

public enum DeviceType
{
    Plc = 0,
    Sensor = 1,
    Instrument = 2,
    Robot = 3,
    Other = 99,
}

public enum ConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Faulted = 3,
}

public enum TagDataType
{
    Bool = 0,
    Int32 = 1,
    Int64 = 2,
    Float = 3,
    Double = 4,
    String = 5,
    Bytes = 6,
}

public enum QualityCode
{
    Good = 0,
    Bad = 1,
    Uncertain = 2,
}

public enum TagDirection
{
    Read = 0,
    Write = 1,
    ReadWrite = 2,
}

public enum AlarmCondition
{
    GreaterThan = 0,
    LessThan = 1,
    Equals = 2,
    NotEquals = 3,
    BitMask = 4,
}

public enum AlarmLevel
{
    Info = 0,
    Warning = 1,
    High = 2,
    Critical = 3,
}
