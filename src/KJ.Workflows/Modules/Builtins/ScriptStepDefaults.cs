namespace KJ.Workflows.Modules.Builtins;

public static class ScriptStepDefaults
{
    public const string Kind = "Script";

    public const string DefaultReferences =
        """
        System.Text.Json
        System.Net.Http
        Microsoft.Extensions.Logging.Abstractions
        KJ.Core
        KJ.Domain
        KJ.Workflows
        """;

    public const string DefaultHandlerScript =
"""
using System.Threading;
using System.Threading.Tasks;
using KJ.Workflows;

/// <summary>实现 IWorkflowStepHandler 的脚本类（可改类名）。</summary>
public sealed class InlineScriptHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) => false;

    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        ctx.Info(step, "脚本已执行。");
        return Task.CompletedTask;
    }
}
""";
}
