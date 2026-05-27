namespace KJ.Workflows.Modules.Builtins;

public sealed class DatabaseStepModule : IWorkflowStepModule
{
    public string Kind => "Database";
    public string Category => "集成";
    public string DisplayName => "数据库";
    public string Description => "执行 SQL 查询或命令";
    public int Order => 33;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("connection", "连接串", "可选，默认应用库"),
        new("sql", "SQL", "SELECT 1"),
    ];

    public void ApplyDefaults(WorkflowStep step) =>
        step.Parameters["sql"] = "SELECT 1";
}
