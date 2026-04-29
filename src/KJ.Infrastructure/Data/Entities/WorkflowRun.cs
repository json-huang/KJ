namespace KJ.Infrastructure.Data.Entities;

public sealed class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    public ICollection<WorkflowRunStep> Steps { get; set; } = new List<WorkflowRunStep>();
}

public sealed class WorkflowRunStep
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public WorkflowRun? Run { get; set; }

    public Guid StepId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
}

