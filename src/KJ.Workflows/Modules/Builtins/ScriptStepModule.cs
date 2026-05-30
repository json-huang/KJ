namespace KJ.Workflows.Modules.Builtins;

public sealed class ScriptStepModule : IWorkflowStepModule
{
    public string Kind => ScriptStepDefaults.Kind;
    public string Category => "脚本";
    public string DisplayName => "C# 脚本";
    public string Description => "编写实现 IWorkflowStepHandler 的 C# 代码，保存后运行流程时动态编译执行。";
    public int Order => 5;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new(
            "references",
            "引用库",
            "每行一个：程序集名 或 dll 路径",
            isReadOnly: false,
            isMultiline: true,
            minLines: 6),
        new(
            "script",
            "脚本代码",
            "实现 IWorkflowStepHandler 的 C# 类",
            isReadOnly: false,
            isMultiline: true,
            minLines: 14),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["references"] = ScriptStepDefaults.DefaultReferences;
        step.Parameters["script"] = ScriptStepDefaults.DefaultHandlerScript;
    }
}
