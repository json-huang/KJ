namespace KJ.Workflows.Modules.Builtins;

public sealed class StartStepModule : IWorkflowStepModule
{
    public string Kind => "Start";
    public string Category => "流程";
    public string DisplayName => "开始";
    public string Description => "流程入口，运行从此步骤出发";
    public int Order => 0;
    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties => [];

    public void ApplyDefaults(WorkflowStep step) { }
}
