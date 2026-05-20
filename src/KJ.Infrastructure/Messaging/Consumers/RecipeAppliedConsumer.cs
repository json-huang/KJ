using MassTransit;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class RecipeAppliedConsumer : IConsumer<RecipeAppliedMessage>
{
    private readonly ILogger<RecipeAppliedConsumer> _logger;
    private readonly KjDbContext _db;

    public RecipeAppliedConsumer(ILogger<RecipeAppliedConsumer> logger, KjDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task Consume(ConsumeContext<RecipeAppliedMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("MassTransit: RecipeApplied RecipeId={RecipeId} DeviceId={DeviceId} User={User}",
            m.RecipeId, m.DeviceId, m.UserId);

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = m.UserId,
            Action = "RecipeApplied",
            Details = $"Recipe {m.RecipeId} applied to device {m.DeviceId}",
        });

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
