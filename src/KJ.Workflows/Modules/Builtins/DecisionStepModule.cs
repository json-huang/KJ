namespace KJ.Workflows.Modules.Builtins;

public sealed class DecisionStepModule : IWorkflowStepModule
{
    public string Kind => "Decision";
    public string Category => "逻辑";
    public string DisplayName => "条件分支";
    public string Description => "按条件选择下一跳步骤";
    public int Order => 20;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("expression", "条件表达式", "例如 tag > 100"),
    ];

    public void ApplyDefaults(WorkflowStep step) =>
        step.Parameters["expression"] = string.Empty;
}
