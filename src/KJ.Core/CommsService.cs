using KJ.Comms.Abstractions;

namespace KJ.Core;

public sealed class CommsService : ICommsService
{
    private readonly ITransport _transport;
    private readonly ITagStore _tagStore;

    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    public CommsService(ITransport transport, ITagStore tagStore)
    {
        _transport = transport;
        _tagStore = tagStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is not null)
            return;

        await _transport.OpenAsync(cancellationToken);
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _loop = Task.Run(async () =>
        {
            var ct = _loopCts.Token;
            while (!ct.IsCancellationRequested)
            {
                // 第一阶段：用“心跳/递增计数”模拟采集数据流，先把 UI 闭环跑通
                _tagStore.Upsert(new TagValue(new TagId("Heartbeat"), DateTimeOffset.Now.ToString("HH:mm:ss.fff"), DateTimeOffset.Now));
                await Task.Delay(500, ct);
            }
        }, _loopCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is null)
            return;

        try
        {
            _loopCts?.Cancel();
            await Task.WhenAny(_loop, Task.Delay(2000, cancellationToken));
        }
        finally
        {
            _loop = null;
            _loopCts?.Dispose();
            _loopCts = null;
            await _transport.CloseAsync(cancellationToken);
        }
    }
}

