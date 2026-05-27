namespace KJ.Workflows.Modules.Builtins;

public sealed class PlcReadStepModule : IWorkflowStepModule
{
    public string Kind => "PlcRead";
    public string Category => "PLC";
    public string DisplayName => "PLC 读";
    public string Description => "按设备与地址读取信号";
    public int Order => 12;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("device", "设备 ID", "必填"),
        new("symbol", "地址/Symbol", "MAIN.nSpeed"),
        new("type", "数据类型", "DINT"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["symbol"] = "MAIN.nSpeed";
        step.Parameters["type"] = "DINT";
    }
}
