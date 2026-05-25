using KJ.Diagnostics;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Polly;
using Polly.Retry;

namespace KJ.Drivers;

/// <summary>
/// OPC UA 驱动 — 基于 OPC Foundation .NET Standard 库实现。
/// 支持连接 OPC UA 服务器、读写节点值。
/// 
/// 地址格式: "ns=2;s=Temperature" 或 "ns=2;i=1001"（标准 OPC UA NodeId）
/// Endpoint: DeviceEndpoint.Host = "opc.tcp://hostname:4840"
/// </summary>
public sealed class OpcUaDriver : IDeviceDriver
{
    public const string DriverTypeConst = "OpcUa";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly DiagnosticHub _diagnostics;
    private Session? _session;
    private ApplicationConfiguration? _appConfig;
    private DeviceEndpoint? _endpoint;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .Build();

    public OpcUaDriver(DiagnosticHub diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            State = DriverConnectionState.Connecting;
            _endpoint = endpoint;

            // 构建 OPC UA 应用配置
            _appConfig = new ApplicationConfiguration()
            {
                ApplicationName = "KJ.OpcUaClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    ApplicationCertificate = new CertificateIdentifier(),
                },
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 10000,
                    MaxStringLength = 65535,
                    MaxByteStringLength = 65535,
                    MaxArrayLength = 65535,
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60000,
                    MinSubscriptionLifetime = 10000,
                },
            };

            await _appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);

            // 解析 endpoint URL
            var endpointUrl = endpoint.Host;
            if (!endpointUrl.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
                endpointUrl = $"opc.tcp://{endpoint.Host}:{endpoint.Port}";

            // 发现并选择最佳 endpoint
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, false);
            var configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(_appConfig));

            // 创建 session
            _session = await Session.Create(
                _appConfig,
                configuredEndpoint,
                false,
                "KJ.OpcUaClient",
                60000,
                new UserIdentity(),
                null,
                cancellationToken).ConfigureAwait(false);

            State = DriverConnectionState.Connected;
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.TransportOpen, nameof(OpcUaDriver),
                Message: $"Connected to OPC UA server at {endpointUrl}"));
        }
        catch (Exception ex)
        {
            State = DriverConnectionState.Faulted;
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(OpcUaDriver),
                Message: $"OPC UA connect failed to {endpoint.Host}:{endpoint.Port}",
                Error: ex.Message));
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_session is not null)
            {
                await _session.CloseAsync(cancellationToken).ConfigureAwait(false);
                _session.Dispose();
                _session = null;
            }
        }
        catch
        {
            // best-effort cleanup
        }

        State = DriverConnectionState.Disconnected;

        if (_endpoint is not null)
        {
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.TransportClose, nameof(OpcUaDriver),
                Message: $"Disconnected from OPC UA server {_endpoint.Host}"));
        }
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _session is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await Retry.ExecuteAsync(async ct =>
            {
                var nodeId = ParseNodeId(request.Address.Address);
                var dataValue = await _session.ReadValueAsync(nodeId, ct).ConfigureAwait(false);

                if (dataValue.StatusCode != StatusCodes.Good)
                {
                    return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false,
                        $"OPC UA status: {dataValue.StatusCode}");
                }

                var value = ConvertToTagValue(dataValue.Value, request.Address.Type);
                return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(OpcUaDriver),
                TagKey: request.TagKey,
                Message: $"OPC UA read failed for {request.Address.Address}",
                Error: ex.Message));
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _session is null)
            throw new InvalidOperationException("Not connected to OPC UA server");

        try
        {
            await Retry.ExecuteAsync(async ct =>
            {
                var nodeId = ParseNodeId(request.Address.Address);
                var dataValue = ToDataValue(request.Value, request.Address.Type);
                var writeValues = new WriteValueCollection { new WriteValue { NodeId = nodeId, AttributeId = Attributes.Value, Value = dataValue } };

                // 使用 Task.Run 包装同步 Write 调用，避免阻塞线程池
                await Task.Run(() =>
                {
                    var results = new StatusCodeCollection();
                    var diagnostics = new DiagnosticInfoCollection();
                    _session!.Write(null, writeValues, out results, out diagnostics);

                    if (results.Count > 0 && results[0] != StatusCodes.Good)
                        throw new InvalidOperationException($"OPC UA write failed: {results[0]}");
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(
                DateTimeOffset.Now, Guid.NewGuid().ToString("N"),
                DiagnosticStage.Exception, nameof(OpcUaDriver),
                TagKey: request.TagKey,
                Message: $"OPC UA write failed for {request.Address.Address}",
                Error: ex.Message));
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static NodeId ParseNodeId(string address)
    {
        // 支持格式: "ns=2;s=Temperature" 或 "ns=2;i=1001"
        try
        {
            return NodeId.Parse(address);
        }
        catch
        {
            // 回退: 假设是字符串标识符
            return new NodeId(address, 2);
        }
    }

    private static object? ConvertToTagValue(object? opcValue, TagValueType targetType)
    {
        if (opcValue is null) return null;

        return targetType switch
        {
            TagValueType.Bool => Convert.ToBoolean(opcValue),
            TagValueType.Int32 => Convert.ToInt32(opcValue),
            TagValueType.Int64 => Convert.ToInt64(opcValue),
            TagValueType.Float => Convert.ToSingle(opcValue),
            TagValueType.Double => Convert.ToDouble(opcValue),
            TagValueType.String => opcValue.ToString(),
            TagValueType.Bytes => opcValue as byte[],
            _ => opcValue,
        };
    }

    private static DataValue ToDataValue(object? value, TagValueType sourceType)
    {
        var variant = sourceType switch
        {
            TagValueType.Bool => new Variant(Convert.ToBoolean(value)),
            TagValueType.Int32 => new Variant(Convert.ToInt32(value)),
            TagValueType.Int64 => new Variant(Convert.ToInt64(value)),
            TagValueType.Float => new Variant(Convert.ToSingle(value)),
            TagValueType.Double => new Variant(Convert.ToDouble(value)),
            TagValueType.String => new Variant(value?.ToString() ?? string.Empty),
            TagValueType.Bytes => new Variant(value as byte[] ?? Array.Empty<byte>()),
            _ => new Variant(value ?? 0),
        };
        return new DataValue(variant);
    }
}
