using KJ.Comms.Abstractions;
using System.Net.Sockets;

namespace KJ.Comms.Drivers.Tcp;

public sealed class TcpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        State = ConnectionState.Connecting;
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, cancellationToken);
            _stream = _client.GetStream();
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Faulted;
            throw;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("TCP stream is not open.");

        await _stream.WriteAsync(payload, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("TCP stream is not open.");

        var buffer = new byte[4096];
        var read = await _stream.ReadAsync(buffer, cancellationToken);
        return buffer.AsMemory(0, read);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
