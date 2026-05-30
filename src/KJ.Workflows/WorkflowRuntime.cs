namespace KJ.Workflows;

public interface IWorkflowStepHandler
{
    bool CanHandle(string kind);
    Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct);
}

public enum WorkflowRunState
{
    Idle = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Canceled = 5,
}

public interface IWorkflowRuntime
{
    WorkflowRunState State { get; }
    Guid? ActiveRunId { get; }
    Guid? ActiveWorkflowId { get; }
    Guid? CurrentStepId { get; }

    event Action? Changed;

    Task<Guid> StartContinuousAsync(WorkflowDefinition workflow, CancellationToken ct = default);
    Task<Guid> StartStepAsync(WorkflowDefinition workflow, CancellationToken ct = default);

    Task StepOnceAsync(CancellationToken ct = default);
    void Pause();
    void Resume();
    void Cancel();
}

public sealed record WorkflowRunResult(Guid RunId, bool Success, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string? Error);

public sealed record WorkflowRunLogEntry(
    DateTimeOffset Timestamp,
    Guid RunId,
    Guid StepId,
    string Kind,
    string Message,
    bool Success,
    string? Error);

public interface IWorkflowRunLogStore
{
    void Append(WorkflowRunLogEntry entry);
    IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200);
}

public sealed class WorkflowExecutionContext
{
    private readonly Action<WorkflowRunLogEntry> _log;

    public WorkflowExecutionContext(Guid runId, Action<WorkflowRunLogEntry> log, IServiceProvider? services = null)
    {
        RunId = runId;
        _log = log;
        Services = services;
    }

    public Guid RunId { get; }
    public IServiceProvider? Services { get; }

    public void Info(WorkflowStep step, string message) =>
        _log(new WorkflowRunLogEntry(DateTimeOffset.Now, RunId, step.Id, step.Kind, message, true, null));

    public void Error(WorkflowStep step, string message, string? error) =>
        _log(new WorkflowRunLogEntry(DateTimeOffset.Now, RunId, step.Id, step.Kind, message, false, error));
}

public interface ITrendPointSink
{
    Task WriteAsync(string tagKey, object? value, DateTimeOffset? timestamp = null, CancellationToken ct = default);
}

public static class WorkflowExecutionContextTrendExtensions
{
    public static Task TrendAsync(this WorkflowExecutionContext ctx, string tagKey, object? value, DateTimeOffset? timestamp = null, CancellationToken ct = default)
    {
        if (ctx.Services is null)
            throw new InvalidOperationException("WorkflowExecutionContext.Services 未初始化，无法写入趋势数据。");

        var sink = ctx.Services.GetService(typeof(ITrendPointSink)) as ITrendPointSink;
        if (sink is null)
            throw new InvalidOperationException("ITrendPointSink 未注册，无法写入趋势数据。");
        return sink.WriteAsync(tagKey, value, timestamp, ct);
    }
}

public sealed class WorkflowRuntimeService : IWorkflowRuntime
{
    private readonly IReadOnlyList<IWorkflowStepHandler> _handlers;
    private readonly IWorkflowRunLogStore _log;
    private readonly IServiceProvider _services;

    private readonly object _gate = new();
    private CancellationTokenSource? _runCts;
    private WorkflowDefinition? _workflow;
    private Dictionary<Guid, WorkflowStep>? _byId;
    private WorkflowStep? _current;
    private readonly Queue<Guid> _pendingStepIds = new();
    private int _remainingBudget;
    private ManualResetEventSlim _pauseGate = new(true);

    private WorkflowRunState _state = WorkflowRunState.Idle;
    public WorkflowRunState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            Changed?.Invoke();
        }
    }

    public Guid? ActiveRunId { get; private set; }
    public Guid? ActiveWorkflowId { get; private set; }
    public Guid? CurrentStepId { get; private set; }

    public event Action? Changed;

    public WorkflowRuntimeService(IEnumerable<IWorkflowStepHandler> handlers, IWorkflowRunLogStore log, IServiceProvider services)
    {
        _handlers = handlers.ToArray();
        _log = log;
        _services = services;
    }

    public async Task<Guid> StartContinuousAsync(WorkflowDefinition workflow, CancellationToken ct = default)
    {
        return await StartInternalAsync(workflow, stepMode: false, ct).ConfigureAwait(false);
    }

    public async Task<Guid> StartStepAsync(WorkflowDefinition workflow, CancellationToken ct = default)
    {
        return await StartInternalAsync(workflow, stepMode: true, ct).ConfigureAwait(false);
    }

    public async Task StepOnceAsync(CancellationToken ct = default)
    {
        WorkflowStep? step;
        WorkflowExecutionContext ctx;
        CancellationToken runToken;

        lock (_gate)
        {
            if (State is WorkflowRunState.Idle or WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Canceled)
                return;

            if (_workflow is null || _byId is null || _current is null || ActiveRunId is null)
                return;

            if (_remainingBudget-- <= 0)
            {
                _current = null;
                return;
            }

            if (State == WorkflowRunState.Paused)
                _pauseGate.Set();

            step = _current;
            runToken = _runCts?.Token ?? CancellationToken.None;
            ctx = new WorkflowExecutionContext(ActiveRunId.Value, _log.Append, _services);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, runToken);
        await ExecuteOneStepAsync(step, ctx, linked.Token).ConfigureAwait(false);

        lock (_gate)
        {
            AdvanceToNextStep(step, ctx);

            if (_current is null)
            {
                _log.Append(new WorkflowRunLogEntry(DateTimeOffset.Now, ActiveRunId!.Value, Guid.Empty, "Run", "Run completed.", true, null));
                State = WorkflowRunState.Completed;
                return;
            }

            if (State == WorkflowRunState.Running || State == WorkflowRunState.Paused)
            {
                // If in step mode, pause after one step.
                _pauseGate.Reset();
                State = WorkflowRunState.Paused;
            }
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (State != WorkflowRunState.Running)
                return;
            _pauseGate.Reset();
            State = WorkflowRunState.Paused;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (State != WorkflowRunState.Paused)
                return;
            _pauseGate.Set();
            State = WorkflowRunState.Running;
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _runCts?.Cancel();
        }
    }

    private async Task<Guid> StartInternalAsync(WorkflowDefinition workflow, bool stepMode, CancellationToken ct)
    {
        lock (_gate)
        {
            // single active run for now
            if (State is WorkflowRunState.Running or WorkflowRunState.Paused)
                throw new InvalidOperationException("A workflow run is already active.");

            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _workflow = workflow;
            _byId = workflow.Steps.ToDictionary(x => x.Id);
            _pendingStepIds.Clear();
            _current = workflow.Steps.FirstOrDefault(s => string.Equals(s.Kind, "Start", StringComparison.OrdinalIgnoreCase)) ?? workflow.Steps.FirstOrDefault();
            _remainingBudget = workflow.Steps.Count + workflow.Links.Count + 5;
            _pauseGate = new ManualResetEventSlim(!stepMode);

            ActiveRunId = Guid.NewGuid();
            ActiveWorkflowId = workflow.Id;
            State = stepMode ? WorkflowRunState.Paused : WorkflowRunState.Running;
        }

        var runId = ActiveRunId!.Value;
        _log.Append(new WorkflowRunLogEntry(DateTimeOffset.Now, runId, Guid.Empty, "Run", $"Run started: {workflow.Name} ({workflow.Id:N})", true, null));

        // if step mode, execute one step immediately then pause
        if (stepMode)
        {
            await StepOnceAsync(ct).ConfigureAwait(false);
            return runId;
        }

        _ = Task.Run(() => RunLoopAsync(runId), CancellationToken.None);
        return runId;
    }

    private async Task RunLoopAsync(Guid runId)
    {
        WorkflowExecutionContext ctx = new(runId, _log.Append, _services);
        try
        {
            while (true)
            {
                WorkflowStep? step;
                CancellationToken ct;

                lock (_gate)
                {
                    if (ActiveRunId != runId)
                        return;
                    ct = _runCts?.Token ?? CancellationToken.None;
                    step = _current;
                    if (_remainingBudget-- <= 0)
                        step = null;
                }

                ct.ThrowIfCancellationRequested();

                if (step is null)
                    break;

                _pauseGate.Wait(ct);

                await ExecuteOneStepAsync(step, ctx, ct).ConfigureAwait(false);

                lock (_gate)
                {
                    AdvanceToNextStep(step, ctx);
                }
            }

            _log.Append(new WorkflowRunLogEntry(DateTimeOffset.Now, runId, Guid.Empty, "Run", "Run completed.", true, null));
            State = WorkflowRunState.Completed;
        }
        catch (OperationCanceledException)
        {
            _log.Append(new WorkflowRunLogEntry(DateTimeOffset.Now, runId, Guid.Empty, "Run", "Run canceled.", false, "Canceled"));
            State = WorkflowRunState.Canceled;
        }
        catch (Exception ex)
        {
            _log.Append(new WorkflowRunLogEntry(DateTimeOffset.Now, runId, Guid.Empty, "Run", "Run failed.", false, ex.ToString()));
            State = WorkflowRunState.Failed;
        }
        finally
        {
            lock (_gate)
            {
                _current = null;
                _byId = null;
                _workflow = null;
                ActiveRunId = null;
                ActiveWorkflowId = null;
                _runCts?.Dispose();
                _runCts = null;
                _pauseGate.Set();
                if (State is not WorkflowRunState.Failed and not WorkflowRunState.Canceled and not WorkflowRunState.Completed)
                    State = WorkflowRunState.Idle;
            }
        }
    }

    private async Task ExecuteOneStepAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        lock (_gate)
        {
            CurrentStepId = step.Id;
        }
        Changed?.Invoke();

        var handler = _handlers.FirstOrDefault(h => h.CanHandle(step.Kind));
        if (handler is null)
        {
            var err = $"No handler for kind '{step.Kind}'.";
            ctx.Error(step, "Step failed.", err);
            throw new InvalidOperationException(err);
        }

        ctx.Info(step, "Step started.");
        try
        {
            await handler.ExecuteAsync(step, ctx, ct).ConfigureAwait(false);
            ctx.Info(step, "Step completed.");
        }
        catch (Exception ex)
        {
            // 关键：确保异常一定写进步骤日志，否则 UI 看起来像“没有错误信息”
            ctx.Error(step, "Step failed.", ex.ToString());
            throw;
        }

        lock (_gate)
        {
            // keep last executed step visible until next step starts / run ends
            CurrentStepId = step.Id;
        }
        Changed?.Invoke();
    }

    private void AdvanceToNextStep(WorkflowStep step, WorkflowExecutionContext ctx)
    {
        if (_workflow is null || _byId is null)
        {
            _current = null;
            return;
        }

        var vars = WorkflowStepNavigator.CollectRuntimeVars(step);
        var outgoing = WorkflowStepNavigator.GetOutgoingStepIds(_workflow, step, ctx, vars);

        if (outgoing.Count > 1)
            ctx.Info(step, $"Fan-out: {outgoing.Count} successor(s) queued.");

        foreach (var id in outgoing)
            _pendingStepIds.Enqueue(id);

        _current = DequeueNextValidStep();
    }

    private WorkflowStep? DequeueNextValidStep()
    {
        if (_byId is null)
            return null;

        while (_pendingStepIds.Count > 0)
        {
            var id = _pendingStepIds.Dequeue();
            if (_byId.TryGetValue(id, out var next))
                return next;
        }

        return null;
    }
}

