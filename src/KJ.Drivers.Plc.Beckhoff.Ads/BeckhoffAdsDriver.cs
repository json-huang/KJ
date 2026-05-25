using System.Text;
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using TwinCAT.Ads;

namespace KJ.Drivers.Plc.Beckhoff.Ads;

/// <summary>
/// Beckhoff ADS PLC 驱动。通过 TwinCAT.Ads 库连接 PLC，读写变量。
/// 
/// 地址格式: "MAIN.iCounter"（PLC 变量名）
/// Endpoint: DeviceEndpoint.Host = AmsNetId (如 "5.80.201.232.1.1")
///           DeviceEndpoint.Port = ADS Port (默认 851，TC3 PLC)
/// </summary>
public sealed class BeckhoffAdsDriver : IDeviceDriver
{
    public const string DriverTypeConst = "Plc.Beckhoff.Ads";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly DiagnosticHub _diagnostics;
    private AdsClient? _client;
    private DeviceEndpoint? _endpoint;
    private readonly Dictionary<string, uint> _handleCache = new();

    public BeckhoffAdsDriver(DiagnosticHub diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            State = DriverConnectionState.Connecting;
            _endpoint = endpoint;

            _client = new AdsClient();
            var amsNetId = AmsNetId.Parse(endpoint.Host);
            var port = endpoint.Port > 0 ? endpoint.Port : 851;

            await _client.ConnectAsync(amsNetId, port, cancellationToken).ConfigureAwait(false);

            var stateInfo = _client.ReadState();
            if (stateInfo.AdsState == AdsState.Error)
                throw new InvalidOperationException($"PLC is in error state: {stateInfo}");

            State = DriverConnectionState.Connected;
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.TransportOpen, nameof(BeckhoffAdsDriver),
                Message: $"Connected to Beckhoff PLC {endpoint.Host}:{port} (State: {stateInfo.AdsState})"));
        }
        catch (Exception ex)
        {
            State = DriverConnectionState.Faulted;
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(BeckhoffAdsDriver),
                Message: $"ADS connect failed to {endpoint.Host}:{endpoint.Port}",
                Error: ex.Message));
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var handle in _handleCache.Values)
            {
                try { _client?.DeleteVariableHandle(handle); } catch { }
            }
            _handleCache.Clear();
            _client?.Dispose();
            _client = null;
        }
        catch { }

        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _client is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            var handle = await GetOrCreateHandleAsync(request.Address.Address, cancellationToken).ConfigureAwait(false);
            var size = GetTypeSize(request.Address.Type);
            var memory = new Memory<byte>(new byte[size]);
            await _client.ReadAsync(handle, memory, cancellationToken).ConfigureAwait(false);
            var value = ConvertFromBytes(memory.Span, request.Address.Type);

            return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(BeckhoffAdsDriver),
                TagKey: request.TagKey,
                Message: $"ADS read failed for {request.Address.Address}",
                Error: ex.Message));
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _client is null)
            throw new InvalidOperationException("Not connected to Beckhoff PLC");

        try
        {
            var handle = await GetOrCreateHandleAsync(request.Address.Address, cancellationToken).ConfigureAwait(false);
            var data = ConvertToBytes(request.Value, request.Address.Type);
            await _client.WriteAsync(handle, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(BeckhoffAdsDriver),
                TagKey: request.TagKey,
                Message: $"ADS write failed for {request.Address.Address}",
                Error: ex.Message));
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<uint> GetOrCreateHandleAsync(string symbolName, CancellationToken ct)
    {
        if (_handleCache.TryGetValue(symbolName, out var cachedHandle))
            return cachedHandle;

        var resultHandle = await _client!.CreateVariableHandleAsync(symbolName, ct).ConfigureAwait(false);
        _handleCache[symbolName] = resultHandle.Handle;
        return resultHandle.Handle;
    }

    private static int GetTypeSize(TagValueType type) => type switch
    {
        TagValueType.Bool => 1,
        TagValueType.Int32 => 4,
        TagValueType.Int64 => 8,
        TagValueType.Float => 4,
        TagValueType.Double => 8,
        TagValueType.String => 256,
        _ => 4,
    };

    private static object? ConvertFromBytes(ReadOnlySpan<byte> data, TagValueType type)
    {
        if (data.IsEmpty) return null;

        return type switch
        {
            TagValueType.Bool => data[0] != 0,
            TagValueType.Int32 => BitConverter.ToInt32(data),
            TagValueType.Int64 => BitConverter.ToInt64(data),
            TagValueType.Float => BitConverter.ToSingle(data),
            TagValueType.Double => BitConverter.ToDouble(data),
            TagValueType.String => Encoding.UTF8.GetString(data).TrimEnd('\0'),
            TagValueType.Bytes => data.ToArray(),
            _ => BitConverter.ToInt32(data),
        };
    }

    private static byte[] ConvertToBytes(object? value, TagValueType type)
    {
        switch (type)
        {
            case TagValueType.Bool:
                return new[] { (byte)(Convert.ToBoolean(value) ? 1 : 0) };
            case TagValueType.Int32:
                return BitConverter.GetBytes(Convert.ToInt32(value));
            case TagValueType.Int64:
                return BitConverter.GetBytes(Convert.ToInt64(value));
            case TagValueType.Float:
                return BitConverter.GetBytes(Convert.ToSingle(value));
            case TagValueType.Double:
                return BitConverter.GetBytes(Convert.ToDouble(value));
            case TagValueType.String:
                return Encoding.UTF8.GetBytes(value?.ToString() ?? string.Empty);
            default:
                return BitConverter.GetBytes(Convert.ToInt32(value));
        }
    }
}
