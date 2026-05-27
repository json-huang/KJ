using System.Text.Json;
using KJ.Domain;
using KJ.Plugin.Contracts;
using KJ.Plugin.Host;
using KJ.Workflows;

namespace KJ.App.Services;

public sealed class PluginHostEventBridge : IDisposable
{
    private readonly PluginManager _pluginManager;
    private readonly ITagStore _tagStore;
    private readonly IAlarmService _alarmService;
    private readonly IWorkflowRuntime _workflowRuntime;

    public PluginHostEventBridge(
        PluginManager pluginManager,
        ITagStore tagStore,
        IAlarmService alarmService,
        IWorkflowRuntime workflowRuntime)
    {
        _pluginManager = pluginManager;
        _tagStore = tagStore;
        _alarmService = alarmService;
        _workflowRuntime = workflowRuntime;

        _tagStore.TagUpdated += OnTagUpdated;
        _alarmService.AlarmRaised += OnAlarmRaised;
        _workflowRuntime.Changed += OnWorkflowRuntimeChanged;
    }

    public void Dispose()
    {
        _tagStore.TagUpdated -= OnTagUpdated;
        _alarmService.AlarmRaised -= OnAlarmRaised;
        _workflowRuntime.Changed -= OnWorkflowRuntimeChanged;
    }

    private void OnTagUpdated(object? sender, TagValue value)
    {
        _ = PublishAsync(PluginProtocol.Topics.TagValueChanged, new
        {
            tagId = value.Id.Value,
            value = value.Value,
            quality = value.Quality.ToString(),
            timestamp = value.Timestamp,
        });
    }

    private void OnAlarmRaised(object? sender, AlarmEvent value)
    {
        _ = PublishAsync(PluginProtocol.Topics.AlarmRaised, new
        {
            code = value.Code,
            message = value.Message,
            severity = value.Severity.ToString(),
            timestamp = value.Timestamp,
        });
    }

    private void OnWorkflowRuntimeChanged()
    {
        _ = PublishAsync(PluginProtocol.Topics.WorkflowRunChanged, new
        {
            state = _workflowRuntime.State.ToString(),
            activeRunId = _workflowRuntime.ActiveRunId,
            activeWorkflowId = _workflowRuntime.ActiveWorkflowId,
            currentStepId = _workflowRuntime.CurrentStepId,
            timestamp = DateTimeOffset.UtcNow,
        });
    }

    private Task PublishAsync(string topic, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return _pluginManager.BroadcastHostEventAsync(topic, json);
    }
}
