namespace KJ.Workflows.Modules.Builtins;

public sealed class ShellStepModule : IWorkflowStepModule
{
    public string Kind => "Shell";
    public string Category => "集成";
    public string DisplayName => "Shell 命令";
    public string Description => "执行本机命令行";
    public int Order => 31;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("command", "命令", "echo"),
        new("args", "参数", "hello"),
        new("workingDir", "工作目录", "可选"),
        new("timeout", "超时(秒)", "60"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["command"] = "echo";
        step.Parameters["args"] = "hello";
        step.Parameters["timeout"] = "60";
    }
}
