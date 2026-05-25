using System.IO.Ports;
using System.Net.Sockets;
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using NModbus;
using NModbus.IO;
using NModbus.Serial;
using Polly;

namespace KJ.Drivers;

// ────────────────────────────────────────────────────────────────────────────
// Shared Modbus address parsing and data conversion helpers
// ────────────────────────────────────────────────────────────────────────────

internal enum ModbusRegisterType { Coil, DiscreteInput, HoldingRegister, InputRegister }

internal readonly record struct ParsedModbusAddress(ModbusRegisterType Type, ushort Address, byte SlaveId = 1);

internal static class ModbusHelpers
{
    /// <summary>
    /// Parse address string like "HR0", "IR50", "C10", "DI5".
    /// Optionally prefixed with slaveId:, e.g. "2:HR100" -> slave 2, holding register 100.
    /// </summary>
    internal static ParsedModbusAddress ParseAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Modbus address cannot be empty.", nameof(address));

        byte slaveId = 1;
        var addr = address.Trim();

        // Check for optional slave-id prefix  "2:HR100"
        var colonIdx = addr.IndexOf(':');
        if (colonIdx > 0 && byte.TryParse(addr[..colonIdx], out var sid))
        {
            slaveId = sid;
            addr = addr[(colonIdx + 1)..];
        }

        // Extract prefix letters and numeric part
        int numStart = 0;
        while (numStart < addr.Length && !char.IsDigit(addr[numStart]))
            numStart++;

        if (numStart == 0 || numStart == addr.Length)
            throw new FormatException($"Invalid Modbus address format: '{address}'. Expected prefix (HR/IR/C/DI) + number, e.g. 'HR100'.");

        var prefix = addr[..numStart].ToUpperInvariant();
        if (!ushort.TryParse(addr[numStart..], out ushort regAddr))
            throw new FormatException($"Invalid register number in Modbus address: '{address}'.");

        return prefix switch
        {
            "HR" => new ParsedModbusAddress(ModbusRegisterType.HoldingRegister, regAddr, slaveId),
            "IR" => new ParsedModbusAddress(ModbusRegisterType.InputRegister, regAddr, slaveId),
            "C" => new ParsedModbusAddress(ModbusRegisterType.Coil, regAddr, slaveId),
            "DI" => new ParsedModbusAddress(ModbusRegisterType.DiscreteInput, regAddr, slaveId),
            _ => throw new FormatException($"Unknown Modbus register prefix '{prefix}' in address '{address}'. Expected HR, IR, C, or DI."),
        };
    }

    /// <summary>
    /// Convert a ushort[] register array to the requested TagValueType.
    /// </summary>
    internal static object? ConvertRegistersToValue(ushort[] registers, TagValueType targetType)
    {
        if (registers.Length == 0) return null;

        return targetType switch
        {
            TagValueType.Bool => registers[0] != 0,
            TagValueType.Int32 => (int)registers[0],
            TagValueType.Int64 => registers.Length >= 2
                ? ((long)registers[0] << 16) | registers[1]
                : (long)registers[0],
            TagValueType.Float => registers.Length >= 2
                ? ConvertRegistersToFloat(registers[0], registers[1])
                : (float)registers[0],
            TagValueType.Double => registers.Length >= 4
                ? ConvertRegistersToDouble(registers)
                : (double)registers[0],
            TagValueType.String => System.Text.Encoding.ASCII.GetString(
                registers.SelectMany(r => new byte[] { (byte)(r >> 8), (byte)(r & 0xFF) }).ToArray()).TrimEnd('\0'),
            TagValueType.Bytes => registers.SelectMany(r => new byte[] { (byte)(r >> 8), (byte)(r & 0xFF) }).ToArray(),
            _ => registers[0],
        };
    }

    private static float ConvertRegistersToFloat(ushort reg0, ushort reg1)
    {
        // Modbus big-endian: reg0 is the most significant word
        byte[] bytes = new byte[4];
        bytes[0] = (byte)(reg0 >> 8);
        bytes[1] = (byte)(reg0 & 0xFF);
        bytes[2] = (byte)(reg1 >> 8);
        bytes[3] = (byte)(reg1 & 0xFF);
        // BitConverter.ToDouble expects little-endian on Windows
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static double ConvertRegistersToDouble(ushort[] registers)
    {
        byte[] bytes = new byte[8];
        for (int i = 0; i < 4; i++)
        {
            bytes[i * 2] = (byte)(registers[i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Convert an object value to ushort[] for writing, based on TagValueType.
    /// </summary>
    internal static ushort[] ConvertValueToRegisters(object? value, TagValueType sourceType)
    {
        switch (sourceType)
        {
            case TagValueType.Bool:
                return new ushort[] { (value is bool b && b) ? (ushort)1 : (ushort)0 };
            case TagValueType.Int32:
                return new ushort[] { (ushort)(Convert.ToInt32(value) & 0xFFFF) };
            case TagValueType.Int64:
            {
                var v = Convert.ToInt64(value);
                return new ushort[] { (ushort)((v >> 16) & 0xFFFF), (ushort)(v & 0xFFFF) };
            }
            case TagValueType.Float:
            {
                var bytes = BitConverter.GetBytes(Convert.ToSingle(value));
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                return new ushort[] { (ushort)((bytes[0] << 8) | bytes[1]), (ushort)((bytes[2] << 8) | bytes[3]) };
            }
            case TagValueType.Double:
            {
                var bytes = BitConverter.GetBytes(Convert.ToDouble(value));
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                return new ushort[]
                {
                    (ushort)((bytes[0] << 8) | bytes[1]),
                    (ushort)((bytes[2] << 8) | bytes[3]),
                    (ushort)((bytes[4] << 8) | bytes[5]),
                    (ushort)((bytes[6] << 8) | bytes[7]),
                };
            }
            case TagValueType.String:
            {
                var str = value?.ToString() ?? string.Empty;
                var strBytes = System.Text.Encoding.ASCII.GetBytes(str);
                if (strBytes.Length % 2 != 0)
                {
                    var padded = new byte[strBytes.Length + 1];
                    strBytes.CopyTo(padded, 0);
                    strBytes = padded;
                }
                var regs = new ushort[strBytes.Length / 2];
                for (int i = 0; i < regs.Length; i++)
                    regs[i] = (ushort)((strBytes[i * 2] << 8) | strBytes[i * 2 + 1]);
                return regs;
            }
            case TagValueType.Bytes when value is byte[] raw:
            {
                var padded = raw;
                if (raw.Length % 2 != 0)
                {
                    padded = new byte[raw.Length + 1];
                    raw.CopyTo(padded, 0);
                }
                var regs = new ushort[padded.Length / 2];
                for (int i = 0; i < regs.Length; i++)
                    regs[i] = (ushort)((padded[i * 2] << 8) | padded[i * 2 + 1]);
                return regs;
            }
            default:
                return new ushort[] { (ushort)(Convert.ToUInt16(value) & 0xFFFF) };
        }
    }
}

// ────────────────────────────────────────────────────────────────────────────
// TcpDeviceDriver  –  raw TCP socket driver (existing)
// ────────────────────────────────────────────────────────────────────────────

public sealed class TcpDeviceDriver : IDeviceDriver
{
    public const string DriverTypeConst = "Tcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly DiagnosticHub _diagnostics;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.FromMilliseconds(200) })
        .Build();

    private static readonly ResiliencePipeline CircuitBreaker = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
        })
        .Build();

    public TcpDeviceDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connecting;
        _client = new TcpClient();
        await _client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
        State = DriverConnectionState.Connected;
        _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
            DiagnosticStage.TransportOpen, "TcpDriver",
            Message: $"Connected to {endpoint.Host}:{endpoint.Port}"));
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                return await Retry.ExecuteAsync(async ct2 =>
                {
                    var addressBytes = System.Text.Encoding.UTF8.GetBytes(request.Address.Address);
                    await _stream.WriteAsync(addressBytes, ct2).ConfigureAwait(false);
                    var buffer = new byte[4096];
                    var read = await _stream.ReadAsync(buffer, ct2).ConfigureAwait(false);
                    var value = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                    return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            throw new InvalidOperationException("Not connected");

        var data = System.Text.Encoding.UTF8.GetBytes(request.Value?.ToString() ?? string.Empty);
        await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// ModbusTcpDriver  –  real NModbus4 TCP/IP implementation
// ────────────────────────────────────────────────────────────────────────────

public sealed class ModbusTcpDriver : IDeviceDriver
{
    public const string DriverTypeConst = "ModbusTcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private DeviceEndpoint? _endpoint;
    private readonly DiagnosticHub _diagnostics;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .Build();

    private static readonly ResiliencePipeline CircuitBreaker = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .Build();

    public ModbusTcpDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            State = DriverConnectionState.Connecting;
            _endpoint = endpoint;

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
            var factory = new ModbusFactory();
            _master = factory.CreateIpMaster(new TcpClientAdapter(_tcpClient));

            State = DriverConnectionState.Connected;
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.TransportOpen, "ModbusTcpDriver",
                Message: $"Connected to {endpoint.Host}:{endpoint.Port}"));
        }
        catch (Exception ex)
        {
            State = DriverConnectionState.Faulted;
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusTcpDriver",
                Message: $"Connect failed to {endpoint.Host}:{endpoint.Port}", Error: ex.Message));
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            (_master as IDisposable)?.Dispose();
            _tcpClient?.Dispose();
        }
        catch { /* best-effort cleanup */ }

        _master = null;
        _tcpClient = null;
        State = DriverConnectionState.Disconnected;

        if (_endpoint is not null)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.TransportClose, "ModbusTcpDriver",
                Message: $"Disconnected from {_endpoint.Host}:{_endpoint.Port}"));
        }

        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _master is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                return await Retry.ExecuteAsync(async _ =>
                {
                    var parsed = ModbusHelpers.ParseAddress(request.Address.Address);
                    var slaveId = parsed.SlaveId;
                    var address = parsed.Address;

                    _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                        DiagnosticStage.DriverRead, "ModbusTcpDriver",
                        TagKey: request.TagKey, Message: $"Reading {request.Address.Address} (slave {slaveId})"));

                    object? value;
                    switch (parsed.Type)
                    {
                        case ModbusRegisterType.Coil:
                            value = (await _master.ReadCoilsAsync(slaveId, address, 1).ConfigureAwait(false))[0];
                            break;
                        case ModbusRegisterType.DiscreteInput:
                            value = (await _master.ReadInputsAsync(slaveId, address, 1).ConfigureAwait(false))[0];
                            break;
                        case ModbusRegisterType.HoldingRegister:
                        {
                            var count = GetRegisterCount(request.Address.Type);
                            var regs = await _master.ReadHoldingRegistersAsync(slaveId, address, count).ConfigureAwait(false);
                            value = ModbusHelpers.ConvertRegistersToValue(regs, request.Address.Type);
                            break;
                        }
                        case ModbusRegisterType.InputRegister:
                        {
                            var count = GetRegisterCount(request.Address.Type);
                            var regs = await _master.ReadInputRegistersAsync(slaveId, address, count).ConfigureAwait(false);
                            value = ModbusHelpers.ConvertRegistersToValue(regs, request.Address.Type);
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Unsupported register type: {parsed.Type}");
                    }

                    return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusTcpDriver",
                TagKey: request.TagKey, Message: $"Read failed for {request.Address.Address}", Error: ex.Message));
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _master is null)
            throw new InvalidOperationException("Not connected");

        try
        {
            await CircuitBreaker.ExecuteAsync(async ct =>
            {
                await Retry.ExecuteAsync(async _ =>
                {
                    var parsed = ModbusHelpers.ParseAddress(request.Address.Address);
                    var slaveId = parsed.SlaveId;
                    var address = parsed.Address;

                    _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                        DiagnosticStage.DriverWrite, "ModbusTcpDriver",
                        TagKey: request.TagKey, Message: $"Writing {request.Address.Address} (slave {slaveId})"));

                    switch (parsed.Type)
                    {
                        case ModbusRegisterType.HoldingRegister:
                        {
                            var regs = ModbusHelpers.ConvertValueToRegisters(request.Value, request.Address.Type);
                            if (regs.Length == 1)
                                await _master.WriteSingleRegisterAsync(slaveId, address, regs[0]).ConfigureAwait(false);
                            else
                                await _master.WriteMultipleRegistersAsync(slaveId, address, regs).ConfigureAwait(false);
                            break;
                        }
                        case ModbusRegisterType.Coil:
                        {
                            var boolVal = request.Value is bool b ? b : Convert.ToBoolean(request.Value);
                            await _master.WriteSingleCoilAsync(slaveId, address, boolVal).ConfigureAwait(false);
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Cannot write to {parsed.Type} register (read-only).");
                    }
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusTcpDriver",
                TagKey: request.TagKey, Message: $"Write failed for {request.Address.Address}", Error: ex.Message));
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private static ushort GetRegisterCount(TagValueType type) => type switch
    {
        TagValueType.Int64 => 2,
        TagValueType.Float => 2,
        TagValueType.Double => 4,
        _ => 1,
    };
}

// ────────────────────────────────────────────────────────────────────────────
// ModbusRtuDriver  –  NModbus4 serial RTU implementation
// ────────────────────────────────────────────────────────────────────────────

public sealed class ModbusRtuDriver : IDeviceDriver
{
    public const string DriverTypeConst = "ModbusRtu";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private SerialPort? _serialPort;
    private IModbusMaster? _master;
    private DeviceEndpoint? _endpoint;
    private readonly DiagnosticHub _diagnostics;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .Build();

    private static readonly ResiliencePipeline CircuitBreaker = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .Build();

    public ModbusRtuDriver(DiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            State = DriverConnectionState.Connecting;
            _endpoint = endpoint;

            // endpoint.Host = COM port name (e.g., "COM1")
            // endpoint.Port = baud rate (e.g., 9600)
            // endpoint.Extra = "dataBits,parity,stopBits" (e.g., "8,None,1")
            int dataBits = 8;
            Parity parity = Parity.None;
            StopBits stopBits = StopBits.One;

            if (!string.IsNullOrWhiteSpace(endpoint.Extra))
            {
                var parts = endpoint.Extra.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 1 && int.TryParse(parts[0], out var db))
                    dataBits = db;
                if (parts.Length >= 2 && Enum.TryParse<Parity>(parts[1], true, out var p))
                    parity = p;
                if (parts.Length >= 3 && parts[2] == "1.5")
                    stopBits = StopBits.OnePointFive;
                else if (parts.Length >= 3 && Enum.TryParse<StopBits>(parts[2], true, out var sb))
                    stopBits = sb;
            }

            _serialPort = new SerialPort(endpoint.Host, endpoint.Port, parity, dataBits, stopBits)
            {
                ReadTimeout = 3000,
                WriteTimeout = 3000,
            };

            // Use Task.Run for the blocking SerialPort.Open() call
            await Task.Run(() => _serialPort.Open(), cancellationToken).ConfigureAwait(false);

            var factory = new ModbusFactory();
            _master = factory.CreateRtuMaster(new SerialPortAdapter(_serialPort));

            State = DriverConnectionState.Connected;
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.TransportOpen, "ModbusRtuDriver",
                Message: $"Connected to {endpoint.Host} @ {endpoint.Port} baud (dataBits={dataBits}, parity={parity}, stopBits={stopBits})"));
        }
        catch (Exception ex)
        {
            State = DriverConnectionState.Faulted;
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusRtuDriver",
                Message: $"Connect failed to {endpoint.Host}", Error: ex.Message));
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            (_master as IDisposable)?.Dispose();
            if (_serialPort?.IsOpen == true)
                _serialPort.Close();
            _serialPort?.Dispose();
        }
        catch { /* best-effort cleanup */ }

        _master = null;
        _serialPort = null;
        State = DriverConnectionState.Disconnected;

        if (_endpoint is not null)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.TransportClose, "ModbusRtuDriver",
                Message: $"Disconnected from {_endpoint.Host}"));
        }

        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _master is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                return await Retry.ExecuteAsync(async _ =>
                {
                    var parsed = ModbusHelpers.ParseAddress(request.Address.Address);
                    var slaveId = parsed.SlaveId;
                    var address = parsed.Address;

                    _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                        DiagnosticStage.DriverRead, "ModbusRtuDriver",
                        TagKey: request.TagKey, Message: $"Reading {request.Address.Address} (slave {slaveId})"));

                    object? value;
                    switch (parsed.Type)
                    {
                        case ModbusRegisterType.Coil:
                            value = (await _master.ReadCoilsAsync(slaveId, address, 1).ConfigureAwait(false))[0];
                            break;
                        case ModbusRegisterType.DiscreteInput:
                            value = (await _master.ReadInputsAsync(slaveId, address, 1).ConfigureAwait(false))[0];
                            break;
                        case ModbusRegisterType.HoldingRegister:
                        {
                            var count = GetRegisterCount(request.Address.Type);
                            var regs = await _master.ReadHoldingRegistersAsync(slaveId, address, count).ConfigureAwait(false);
                            value = ModbusHelpers.ConvertRegistersToValue(regs, request.Address.Type);
                            break;
                        }
                        case ModbusRegisterType.InputRegister:
                        {
                            var count = GetRegisterCount(request.Address.Type);
                            var regs = await _master.ReadInputRegistersAsync(slaveId, address, count).ConfigureAwait(false);
                            value = ModbusHelpers.ConvertRegistersToValue(regs, request.Address.Type);
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Unsupported register type: {parsed.Type}");
                    }

                    return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusRtuDriver",
                TagKey: request.TagKey, Message: $"Read failed for {request.Address.Address}", Error: ex.Message));
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _master is null)
            throw new InvalidOperationException("Not connected");

        try
        {
            await CircuitBreaker.ExecuteAsync(async ct =>
            {
                await Retry.ExecuteAsync(async _ =>
                {
                    var parsed = ModbusHelpers.ParseAddress(request.Address.Address);
                    var slaveId = parsed.SlaveId;
                    var address = parsed.Address;

                    _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                        DiagnosticStage.DriverWrite, "ModbusRtuDriver",
                        TagKey: request.TagKey, Message: $"Writing {request.Address.Address} (slave {slaveId})"));

                    switch (parsed.Type)
                    {
                        case ModbusRegisterType.HoldingRegister:
                        {
                            var regs = ModbusHelpers.ConvertValueToRegisters(request.Value, request.Address.Type);
                            if (regs.Length == 1)
                                await _master.WriteSingleRegisterAsync(slaveId, address, regs[0]).ConfigureAwait(false);
                            else
                                await _master.WriteMultipleRegistersAsync(slaveId, address, regs).ConfigureAwait(false);
                            break;
                        }
                        case ModbusRegisterType.Coil:
                        {
                            var boolVal = request.Value is bool b ? b : Convert.ToBoolean(request.Value);
                            await _master.WriteSingleCoilAsync(slaveId, address, boolVal).ConfigureAwait(false);
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Cannot write to {parsed.Type} register (read-only).");
                    }
                }, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Publish(new DiagnosticEvent(DateTimeOffset.Now, Guid.NewGuid().ToString(),
                DiagnosticStage.Exception, "ModbusRtuDriver",
                TagKey: request.TagKey, Message: $"Write failed for {request.Address.Address}", Error: ex.Message));
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private static ushort GetRegisterCount(TagValueType type) => type switch
    {
        TagValueType.Int64 => 2,
        TagValueType.Float => 2,
        TagValueType.Double => 4,
        _ => 1,
    };
}

// ────────────────────────────────────────────────────────────────────────────
// OpcUaDriver  –  see OpcUaDriver.cs for implementation
// ────────────────────────────────────────────────────────────────────────────

// ────────────────────────────────────────────────────────────────────────────
// DeviceDriverFactory  –  updated to include ModbusRtu
// ────────────────────────────────────────────────────────────────────────────

public sealed class DeviceDriverFactory : IDeviceDriverFactory
{
    private readonly IServiceProvider _services;

    public DeviceDriverFactory(IServiceProvider services) => _services = services;

    public IDeviceDriver Create(string driverType) => driverType switch
    {
        TcpDeviceDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(TcpDeviceDriver))!,
        ModbusTcpDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(ModbusTcpDriver))!,
        ModbusRtuDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(ModbusRtuDriver))!,
        OpcUaDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(OpcUaDriver))!,
        // Beckhoff ADS and other extension drivers are resolved by convention from IServiceProvider
        _ => ResolveExtensionDriver(driverType),
    };

    private IDeviceDriver ResolveExtensionDriver(string driverType)
    {
        // 从 DI 容器中查找所有已注册的 IDeviceDriver，按 DriverType 匹配
        if (_services.GetService(typeof(IEnumerable<IDeviceDriver>)) is IEnumerable<IDeviceDriver> drivers)
        {
            var match = drivers.FirstOrDefault(d => d.DriverType.Equals(driverType, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        throw new NotSupportedException(
            $"Unknown driver type: '{driverType}'. " +
            $"Supported: {string.Join(", ", GetSupportedDrivers())}");
    }

    public IReadOnlyList<string> GetSupportedDrivers() =>
        new[] { TcpDeviceDriver.DriverTypeConst, ModbusTcpDriver.DriverTypeConst, ModbusRtuDriver.DriverTypeConst, OpcUaDriver.DriverTypeConst };
}
