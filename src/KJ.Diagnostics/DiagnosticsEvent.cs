namespace KJ.Diagnostics;

public enum DiagnosticStage
{
    TransportOpen = 0,
    TransportClose = 1,
    TransportSend = 2,
    TransportReceive = 3,
    ProtocolEncode = 4,
    ProtocolDecode = 5,
    DriverRead = 6,
    DriverWrite = 7,
    Retry = 8,
    Exception = 9,
}

public sealed record DiagnosticEvent(
    DateTimeOffset Timestamp,
    string TraceId,
    DiagnosticStage Stage,
    string Source,
    string? DeviceId = null,
    string? TagKey = null,
    string? Direction = null,
    int? DurationMs = null,
    string? Hex = null,
    string? Message = null,
    string? Error = null,
    IReadOnlyDictionary<string, string>? Meta = null);

