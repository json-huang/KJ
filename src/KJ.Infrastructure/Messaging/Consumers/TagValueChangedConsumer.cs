using MassTransit;
using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class TagValueChangedConsumer : IConsumer<TagValueChangedMessage>
{
    private readonly ILogger<TagValueChangedConsumer> _logger;
    private readonly KjDbContext _db;
    private readonly ITagStore _tagStore;
    private readonly IAlarmService _alarmService;

    public TagValueChangedConsumer(
        ILogger<TagValueChangedConsumer> logger,
        KjDbContext db,
        ITagStore tagStore,
        IAlarmService alarmService)
    {
        _logger = logger;
        _db = db;
        _tagStore = tagStore;
        _alarmService = alarmService;
    }

    public async Task Consume(ConsumeContext<TagValueChangedMessage> context)
    {
        var m = context.Message;
        _logger.LogDebug(
            "MassTransit: TagValueChanged TagId={TagId} TagKey={TagKey} Quality={Quality} Value={Value}",
            m.TagId,
            m.TagKey,
            m.Quality,
            m.Value);

        await EnsureSimulatedDeviceAndTagAsync(m.TagId, m.TagKey, context.CancellationToken).ConfigureAwait(false);

        var nowLocal = m.Timestamp.ToLocalTime().DateTime;
        var quality = m.Quality switch
        {
            TagQualityDto.Good => QualityCode.Good,
            TagQualityDto.Bad => QualityCode.Bad,
            _ => QualityCode.Uncertain,
        };

        var tag = await _db.Tags.FirstAsync(t => t.Id == m.TagId, context.CancellationToken).ConfigureAwait(false);
        tag.Value = m.Value?.ToString();
        tag.Quality = quality;
        tag.Timestamp = nowLocal;

        _db.TagHistory.Add(new TagHistory
        {
            Id = Guid.NewGuid(),
            TagId = m.TagId,
            Timestamp = nowLocal,
            Value = m.Value?.ToString(),
            Quality = quality,
        });

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        _tagStore.Upsert(new TagValue(
            new TagId(m.TagKey),
            m.Value,
            m.Quality switch
            {
                TagQualityDto.Good => TagQuality.Good,
                TagQualityDto.Bad => TagQuality.Bad,
                _ => TagQuality.Unknown,
            },
            m.Timestamp));

        if (m.Quality != TagQualityDto.Good)
        {
            _alarmService.Raise(new AlarmEvent(
                Code: "tag:quality",
                Message: $"Tag '{m.TagKey}' quality={m.Quality}",
                Severity: m.Quality == TagQualityDto.Bad ? AlarmSeverity.Warning : AlarmSeverity.Info,
                Timestamp: m.Timestamp));
        }
    }

    private async Task EnsureSimulatedDeviceAndTagAsync(Guid tagId, string tagKey, CancellationToken cancellationToken)
    {
        if (!await _db.Devices.AnyAsync(d => d.Id == TagIdentity.SimulatedDeviceId, cancellationToken).ConfigureAwait(false))
        {
            _db.Devices.Add(new Device
            {
                Id = TagIdentity.SimulatedDeviceId,
                Name = "Simulated",
                Description = "Auto-created for local TagValueChanged pipeline.",
                Type = DeviceType.Plc,
                State = ConnectionState.Connected,
                LastConnected = DateTime.Now,
                Address = new DeviceAddress { Host = "127.0.0.1", Port = 0 },
                PropertiesJson = "{}",
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!await _db.Tags.AnyAsync(t => t.Id == tagId, cancellationToken).ConfigureAwait(false))
        {
            _db.Tags.Add(new Tag
            {
                Id = tagId,
                DeviceId = TagIdentity.SimulatedDeviceId,
                Name = tagKey,
                DataType = TagDataType.String,
                Address = tagKey,
                Quality = QualityCode.Uncertain,
                Timestamp = DateTime.Now,
                Direction = TagDirection.Read,
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
