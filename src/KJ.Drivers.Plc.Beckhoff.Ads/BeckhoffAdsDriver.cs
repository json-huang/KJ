using KJ.Diagnostics;
using KJ.Drivers.Abstractions;

namespace KJ.Drivers.Plc.Beckhoff.Ads;

/// <summary>
/// 默认占位实现：先把“架构/诊断/配置形态”做出来。
/// 后续你确定 AmsNetId/AmsPort/路由后，再替换为真正的 ADS 读写实现。
/// </summary>
public sealed class BeckhoffAdsDriver : IDeviceDriver
{
    private readonly DiagnosticHub _diag;
    private DeviceEndpoint? _endpoint;

    public BeckhoffAdsDriver(DiagnosticHub diag)
    {
        _diag = diag;
    }

    public string DriverType => "Plc.Beckhoff.Ads";

    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString("N");
        State = DriverConnectionState.Connecting;
        _endpoint = endpoint;

        _diag.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.TransportOpen,
            Source: DriverType,
            DeviceId: endpoint.Host,
            Message: $"Connect placeholder to {endpoint.Host}:{endpoint.Port} Extra={endpoint.Extra}"));

        State = DriverConnectionState.Connected;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString("N");
        State = DriverConnectionState.Disconnected;

        _diag.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.TransportClose,
            Source: DriverType,
            DeviceId: _endpoint?.Host,
            Message: "Disconnect placeholder"));

        _endpoint = null;
        return Task.CompletedTask;
    }

    public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString("N");
        _diag.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.DriverRead,
            Source: DriverType,
            DeviceId: _endpoint?.Host,
            TagKey: request.TagKey,
            Message: $"Read placeholder Address={request.Address.Address} Type={request.Address.Type}"));

        // 默认返回“未实现”，用于把链路打通；后续替换为真实 ADS 读值。
        return Task.FromResult(new TagReadResult(
            TagKey: request.TagKey,
            Value: null,
            Timestamp: DateTimeOffset.Now,
            Success: false,
            Error: "ADS driver placeholder: not implemented."));
    }

    public Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString("N");
        _diag.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.DriverWrite,
            Source: DriverType,
            DeviceId: _endpoint?.Host,
            TagKey: request.TagKey,
            Message: $"Write placeholder Address={request.Address.Address} Type={request.Address.Type} Value={request.Value}"));

        throw new NotSupportedException("ADS driver placeholder: write not implemented.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

