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
        new("device", "设备 ID", "与配置模块一致"),
        new("amsNetId", "AmsNetId", "192.168.1.10.1.1"),
        new("amsPort", "ADS Port", "851"),
        new("symbol", "Symbol", "GVL.bRun"),
        new("type", "数据类型", "BOOL/DINT…"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["amsNetId"] = "192.168.1.10.1.1";
        step.Parameters["amsPort"] = "851";
        step.Parameters["symbol"] = "GVL.bRun";
        step.Parameters["type"] = "DINT";
    }
}
