using MassTransit;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class AlarmTriggeredConsumer : IConsumer<AlarmTriggeredMessage>
{
    private readonly ILogger<AlarmTriggeredConsumer> _logger;

    public AlarmTriggeredConsumer(ILogger<AlarmTriggeredConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<AlarmTriggeredMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("MassTransit: AlarmTriggered AlarmId={AlarmId} TagId={TagId} Level={Level}", m.AlarmId, m.TagId, m.Level);
        return Task.CompletedTask;
    }
}
