using KJ.Workflows;

namespace KJ.Infrastructure.Workflows;

public sealed class StartStepHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) => string.Equals(kind, "Start", StringComparison.OrdinalIgnoreCase);

    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        ctx.Info(step, "Start");
        return Task.CompletedTask;
    }
}

