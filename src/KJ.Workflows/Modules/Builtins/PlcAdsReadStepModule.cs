namespace KJ.Workflows.Modules.Builtins;

public sealed class PlcAdsReadStepModule : IWorkflowStepModule
{
    public string Kind => "Plc.Ads.Read";
    public string Category => "PLC";
    public string DisplayName => "ADS 读";
    public string Description => "通过 Beckhoff ADS 读取变量";
    public int Order => 10;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("device", "设备", "从设备配置中选择（Host/Port 在设备里配置）"),
        new("symbol", "Symbol", "GVL.bRun"),
        new("type", "数据类型", "BOOL/DINT…"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["symbol"] = "GVL.bRun";
        step.Parameters["type"] = "DINT";
    }
}
