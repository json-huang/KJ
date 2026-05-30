namespace KJ.Workflows.Modules;

/// <summary>步骤属性字段定义（用于属性面板动态生成）。</summary>
public sealed class WorkflowStepPropertyDefinition
{
    public WorkflowStepPropertyDefinition(
        string key,
        string label,
        string? placeholder = null,
        bool isReadOnly = false,
        bool isMultiline = false,
        int minLines = 3)
    {
        Key = key;
        Label = label;
        Placeholder = placeholder;
        IsReadOnly = isReadOnly;
        IsMultiline = isMultiline;
        MinLines = Math.Max(1, minLines);
    }

    public string Key { get; }
    public string Label { get; }
    public string? Placeholder { get; }
    public bool IsReadOnly { get; }
    public bool IsMultiline { get; }
    public int MinLines { get; }
}
