using MassTransit;
using KJ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class DeviceStateChangedConsumer : IConsumer<DeviceStateChangedMessage>
{
    private readonly ILogger<DeviceStateChangedConsumer> _logger;
    private readonly KjDbContext _db;

    public DeviceStateChangedConsumer(ILogger<DeviceStateChangedConsumer> logger, KjDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task Consume(ConsumeContext<DeviceStateChangedMessage> context)
    {
        var m = context.Message;
        _logger.LogDebug("MassTransit: DeviceStateChanged DeviceId={DeviceId} State={State}", m.DeviceId, m.State);

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == m.DeviceId, context.CancellationToken).ConfigureAwait(false);
        if (device is null)
            return;

        device.State = m.State switch
        {
            ConnectionStateDto.Connected => Data.Entities.ConnectionState.Connected,
            ConnectionStateDto.Connecting => Data.Entities.ConnectionState.Connecting,
            ConnectionStateDto.Faulted => Data.Entities.ConnectionState.Faulted,
            _ => Data.Entities.ConnectionState.Disconnected,
        };
        device.LastConnected = DateTime.Now;

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
