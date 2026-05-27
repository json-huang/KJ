namespace KJ.Workflows.Modules.Builtins;

public sealed class PlcWriteStepModule : IWorkflowStepModule
{
    public string Kind => "PlcWrite";
    public string Category => "PLC";
    public string DisplayName => "PLC 写";
    public string Description => "按设备与地址写入信号";
    public int Order => 13;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("device", "设备 ID", "必填"),
        new("symbol", "地址/Symbol", "GVL.bRun"),
        new("type", "数据类型", "BOOL"),
        new("value", "写入值", "true/false 或数值"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["symbol"] = "GVL.bRun";
        step.Parameters["type"] = "BOOL";
        step.Parameters["value"] = "true";
    }
}
