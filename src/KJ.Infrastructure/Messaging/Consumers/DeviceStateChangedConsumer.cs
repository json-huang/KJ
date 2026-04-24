using MassTransit;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class DeviceStateChangedConsumer : IConsumer<DeviceStateChangedMessage>
{
    private readonly ILogger<DeviceStateChangedConsumer> _logger;

    public DeviceStateChangedConsumer(ILogger<DeviceStateChangedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<DeviceStateChangedMessage> context)
    {
        var m = context.Message;
        _logger.LogDebug("MassTransit: DeviceStateChanged DeviceId={DeviceId} State={State}", m.DeviceId, m.State);
        return Task.CompletedTask;
    }
}
