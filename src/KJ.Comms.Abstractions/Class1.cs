using System.Buffers;

namespace KJ.Comms.Abstractions;

public enum ConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Faulted = 3,
}

public interface IConnection : IAsyncDisposable
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public interface ITransport : IAsyncDisposable
{
    ConnectionState State { get; }

    Task OpenAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);

    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken = default);
}

public interface IProtocol
{
    ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> appPayload);
    bool TryDecode(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> appPayload);
}

public readonly record struct TagId(string Value);

public readonly record struct TagValue(TagId Id, object? Value, DateTimeOffset Timestamp);

public interface ITagStore
{
    event EventHandler<TagValue>? TagUpdated;
    bool TryGet(TagId id, out TagValue value);
    void Upsert(TagValue value);
}

public interface ICommsService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
