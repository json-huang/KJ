using KJ.Comms.Abstractions;
using System.Net.Sockets;
using KJ.Diagnostics;
using System.Diagnostics;

namespace KJ.Comms.Drivers.Tcp;

public sealed class TcpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly DiagnosticHub? _diag;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpTransport(string host, int port, DiagnosticHub? diag = null)
    {
        _host = host;
        _port = port;
        _diag = diag;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        State = ConnectionState.Connecting;
        var traceId = Guid.NewGuid().ToString("N");
        var sw = Stopwatch.StartNew();
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, cancellationToken);
            _stream = _client.GetStream();
            State = ConnectionState.Connected;

            _diag?.Publish(new DiagnosticEvent(
                Timestamp: DateTimeOffset.Now,
                TraceId: traceId,
                Stage: DiagnosticStage.TransportOpen,
                Source: "TcpTransport",
                DeviceId: $"{_host}:{_port}",
                DurationMs: (int)sw.ElapsedMilliseconds));
        }
        catch
        {
            State = ConnectionState.Faulted;
            _diag?.Publish(new DiagnosticEvent(
                Timestamp: DateTimeOffset.Now,
                TraceId: traceId,
                Stage: DiagnosticStage.Exception,
                Source: "TcpTransport",
                DeviceId: $"{_host}:{_port}",
                DurationMs: (int)sw.ElapsedMilliseconds,
                Error: "OpenAsync failed"));
            throw;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString("N");
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
        State = ConnectionState.Disconnected;

        _diag?.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.TransportClose,
            Source: "TcpTransport",
            DeviceId: $"{_host}:{_port}"));
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("TCP stream is not open.");

        var traceId = Guid.NewGuid().ToString("N");
        var sw = Stopwatch.StartNew();
        await _stream.WriteAsync(payload, cancellationToken);
        await _stream.FlushAsync(cancellationToken);

        _diag?.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.TransportSend,
            Source: "TcpTransport",
            DeviceId: $"{_host}:{_port}",
            Direction: "Out",
            DurationMs: (int)sw.ElapsedMilliseconds,
            Hex: Convert.ToHexString(payload.ToArray())));
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("TCP stream is not open.");

        var traceId = Guid.NewGuid().ToString("N");
        var sw = Stopwatch.StartNew();
        var buffer = new byte[4096];
        var read = await _stream.ReadAsync(buffer, cancellationToken);
        var result = buffer.AsMemory(0, read);

        _diag?.Publish(new DiagnosticEvent(
            Timestamp: DateTimeOffset.Now,
            TraceId: traceId,
            Stage: DiagnosticStage.TransportReceive,
            Source: "TcpTransport",
            DeviceId: $"{_host}:{_port}",
            Direction: "In",
            DurationMs: (int)sw.ElapsedMilliseconds,
            Hex: Convert.ToHexString(result.ToArray())));

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
