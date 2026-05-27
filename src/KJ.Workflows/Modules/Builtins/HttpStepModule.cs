namespace KJ.Workflows.Modules.Builtins;

public sealed class HttpStepModule : IWorkflowStepModule
{
    public string Kind => "Http";
    public string Category => "集成";
    public string DisplayName => "HTTP 请求";
    public string Description => "调用外部 REST API";
    public int Order => 30;

    public IReadOnlyList<WorkflowStepPropertyDefinition> Properties { get; } =
    [
        new("url", "URL", "https://"),
        new("method", "Method", "GET/POST/PUT"),
        new("body", "Body", "JSON 请求体"),
        new("timeout", "超时(秒)", "30"),
    ];

    public void ApplyDefaults(WorkflowStep step)
    {
        step.Parameters["url"] = "https://";
        step.Parameters["method"] = "GET";
        step.Parameters["timeout"] = "30";
    }
}
