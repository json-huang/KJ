using MassTransit;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class TagValueChangedConsumer : IConsumer<TagValueChangedMessage>
{
    private readonly ILogger<TagValueChangedConsumer> _logger;

    public TagValueChangedConsumer(ILogger<TagValueChangedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<TagValueChangedMessage> context)
    {
        var m = context.Message;
        _logger.LogDebug("MassTransit: TagValueChanged TagId={TagId} Value={Value}", m.TagId, m.Value);
        return Task.CompletedTask;
    }
}
