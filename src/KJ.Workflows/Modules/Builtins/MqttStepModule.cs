namespace KJ.Workflows.Modules.Builtins;

public sealed class MqttStepModule : IWorkflowStepModule
{
    public string Kind => "Mqtt";
    public string Category => "集成";
    public string DisplayName => "MQTT 发布";
    public string Description => "向 MQTT 主题发布消息";
    public int Order => 32;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("broker", "Broker", "tcp://localhost:1883"),
        new("topic", "主题", "kj/workflow"),
        new("payload", "消息体", "{}"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["topic"] = "kj/workflow";
        step.Parameters["payload"] = "{}";
    }
}
