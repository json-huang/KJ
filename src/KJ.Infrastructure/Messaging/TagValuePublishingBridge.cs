using KJ.Domain;
using KJ.Infrastructure.Data;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging;

/// <summary>
/// Bridges in-memory tag updates to MassTransit messages.
/// </summary>
public sealed class TagValuePublishingBridge : IDisposable
{
    private readonly ITagStore _tagStore;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<TagValuePublishingBridge> _logger;

    public TagValuePublishingBridge(
        ITagStore tagStore,
        IPublishEndpoint publishEndpoint,
        ILogger<TagValuePublishingBridge> logger)
    {
        _tagStore = tagStore;
        _publishEndpoint = publishEndpoint;
        _logger = logger;

        _tagStore.TagUpdated += OnTagUpdated;
    }

    private void OnTagUpdated(object? sender, TagValue value)
    {
        _ = PublishAsync(value);
    }

    private async Task PublishAsync(TagValue value)
    {
        try
        {
            var tagKey = value.Id.Value;
            var tagId = TagIdentity.GetTagId(tagKey);
            await _publishEndpoint.Publish(new TagValueChangedMessage(
                TagId: tagId,
                TagKey: tagKey,
                Value: value.Value,
                Timestamp: value.Timestamp,
                Quality: value.Quality switch
                {
                    TagQuality.Good => TagQualityDto.Good,
                    TagQuality.Bad => TagQualityDto.Bad,
                    _ => TagQualityDto.Unknown,
                })).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish TagValueChangedMessage.");
        }
    }

    public void Dispose()
    {
        _tagStore.TagUpdated -= OnTagUpdated;
    }
}

