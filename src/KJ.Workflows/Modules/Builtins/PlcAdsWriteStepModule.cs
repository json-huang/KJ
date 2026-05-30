namespace KJ.Workflows.Modules.Builtins;

public sealed class PlcAdsWriteStepModule : IWorkflowStepModule
{
    public string Kind => "Plc.Ads.Write";
    public string Category => "PLC";
    public string DisplayName => "ADS 写";
    public string Description => "通过 Beckhoff ADS 写入变量";
    public int Order => 11;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("device", "设备", "从设备配置中选择（Host/Port 在设备里配置）"),
        new("symbol", "Symbol", "GVL.bRun"),
        new("type", "数据类型", "BOOL/DINT…"),
        new("value", "写入值", "写步骤用"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["symbol"] = "GVL.bRun";
        step.Parameters["type"] = "BOOL";
        step.Parameters["value"] = "true";
    }
}
