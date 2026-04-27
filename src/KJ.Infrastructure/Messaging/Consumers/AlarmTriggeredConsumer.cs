using MassTransit;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class AlarmTriggeredConsumer : IConsumer<AlarmTriggeredMessage>
{
    private readonly ILogger<AlarmTriggeredConsumer> _logger;
    private readonly KjDbContext _db;

    public AlarmTriggeredConsumer(ILogger<AlarmTriggeredConsumer> logger, KjDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task Consume(ConsumeContext<AlarmTriggeredMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("MassTransit: AlarmTriggered AlarmId={AlarmId} TagId={TagId} Level={Level}", m.AlarmId, m.TagId, m.Level);

        var alarm = await _db.Alarms.FirstOrDefaultAsync(a => a.Id == m.AlarmId, context.CancellationToken).ConfigureAwait(false);
        if (alarm is null)
        {
            alarm = new Alarm
            {
                Id = m.AlarmId,
                TagId = m.TagId,
                Name = $"Alarm:{m.TagId:N}",
                Condition = AlarmCondition.Equals,
                Level = m.Level switch
                {
                    AlarmLevelDto.Critical => AlarmLevel.Critical,
                    AlarmLevelDto.High => AlarmLevel.High,
                    AlarmLevelDto.Warning => AlarmLevel.Warning,
                    _ => AlarmLevel.Info,
                },
                IsEnabled = true,
                TriggeredAt = DateTime.Now,
            };
            _db.Alarms.Add(alarm);
        }

        _db.AlarmHistory.Add(new AlarmHistory
        {
            Id = Guid.NewGuid(),
            AlarmId = alarm.Id,
            Timestamp = DateTime.Now,
            EventType = "Triggered",
            Message = m.Message,
            UserId = null,
        });

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
