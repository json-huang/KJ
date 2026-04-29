using MassTransit;
using KJ.Workflows;
using KJ.Infrastructure.Messaging;
using KJ.Infrastructure.Data;

namespace KJ.Infrastructure.Workflows;

public sealed class SimAdsReadStepHandler : IWorkflowStepHandler
{
    private readonly IPublishEndpoint _publish;

    public SimAdsReadStepHandler(IPublishEndpoint publish) => _publish = publish;

    public bool CanHandle(string kind) => string.Equals(kind, "Plc.Ads.Read", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var symbol = step.Parameters.TryGetValue("symbol", out var s) ? s : step.Title;
        var tagKey = $"ads:{symbol}";
        var value = $"sim:{DateTimeOffset.Now:HH:mm:ss.fff}";

        ctx.Info(step, $"Sim ADS read: {tagKey} = {value}");

        await _publish.Publish(
            new TagValueChangedMessage(
                TagId: TagIdentity.GetTagId(tagKey),
                TagKey: tagKey,
                Value: value,
                Timestamp: DateTimeOffset.Now,
                Quality: TagQualityDto.Good),
            ct).ConfigureAwait(false);
    }
}

public sealed class SimAdsWriteStepHandler : IWorkflowStepHandler
{
    private readonly IPublishEndpoint _publish;

    public SimAdsWriteStepHandler(IPublishEndpoint publish) => _publish = publish;

    public bool CanHandle(string kind) => string.Equals(kind, "Plc.Ads.Write", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var symbol = step.Parameters.TryGetValue("symbol", out var s) ? s : step.Title;
        var tagKey = $"ads:{symbol}";
        var value = step.Parameters.TryGetValue("value", out var v) ? v : "true";

        ctx.Info(step, $"Sim ADS write: {tagKey} <= {value}");

        await _publish.Publish(
            new TagValueChangedMessage(
                TagId: TagIdentity.GetTagId(tagKey),
                TagKey: tagKey,
                Value: value,
                Timestamp: DateTimeOffset.Now,
                Quality: TagQualityDto.Good),
            ct).ConfigureAwait(false);
    }
}

