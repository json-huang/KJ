namespace KJ.Workflows.Modules;

/// <summary>步骤属性字段定义（用于属性面板动态生成）。</summary>
public sealed class WorkflowStepPropertyDefinition
{
    public WorkflowStepPropertyDefinition(
        string key,
        string label,
        string? placeholder = null,
        bool isReadOnly = false)
    {
        Key = key;
        Label = label;
        Placeholder = placeholder;
        IsReadOnly = isReadOnly;
    }

    public string Key { get; }
    public string Label { get; }
    public string? Placeholder { get; }
    public bool IsReadOnly { get; }
}
