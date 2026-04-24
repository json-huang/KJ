using MassTransit;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class RecipeAppliedConsumer : IConsumer<RecipeAppliedMessage>
{
    private readonly ILogger<RecipeAppliedConsumer> _logger;

    public RecipeAppliedConsumer(ILogger<RecipeAppliedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<RecipeAppliedMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("MassTransit: RecipeApplied RecipeId={RecipeId} DeviceId={DeviceId} User={User}", m.RecipeId, m.DeviceId, m.UserId);
        return Task.CompletedTask;
    }
}
