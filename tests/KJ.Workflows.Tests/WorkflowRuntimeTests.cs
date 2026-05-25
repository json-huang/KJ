using FluentAssertions;
using Xunit;

namespace KJ.Workflows.Tests;

public class WorkflowRuntimeTests
{
    private static WorkflowDefinition MakeWorkflow(params (string title, string kind, Guid? next)[] steps)
    {
        var stepList = new List<WorkflowStep>();
        for (int i = 0; i < steps.Length; i++)
        {
            stepList.Add(new WorkflowStep
            {
                Id = Guid.NewGuid(),
                Title = steps[i].title,
                Kind = steps[i].kind,
                NextStepId = steps[i].next,
            });
        }
        for (int i = 0; i < stepList.Count - 1; i++)
        {
            if (stepList[i].NextStepId is null)
                stepList[i].NextStepId = stepList[i + 1].Id;
        }
        return new WorkflowDefinition { Id = Guid.NewGuid(), Name = "TestWorkflow", Steps = stepList };
    }

    private static WorkflowRuntimeService CreateRuntime(params IWorkflowStepHandler[] handlers)
    {
        var logStore = new InMemoryRunLogStore();
        return new WorkflowRuntimeService(handlers, logStore);
    }

    private sealed class TestStepHandler : IWorkflowStepHandler
    {
        private readonly string[] _kinds;
        private readonly List<string> _executed = new();
        private readonly object _gate = new();
        public Func<WorkflowStep, WorkflowExecutionContext, CancellationToken, Task>? OnExecute { get; set; }

        public TestStepHandler(params string[] kinds) => _kinds = kinds.Length > 0 ? kinds : new[] { "Action", "Start" };
        public bool CanHandle(string kind) => _kinds.Contains(kind);

        public string[] ExecutedSteps { get { lock (_gate) return _executed.ToArray(); } }

        public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
        {
            lock (_gate) _executed.Add(step.Title);
            if (OnExecute is not null) await OnExecute(step, ctx, ct);
            ctx.Info(step, "done");
        }
    }

    private sealed class FailingStepHandler : IWorkflowStepHandler
    {
        public bool CanHandle(string kind) => kind == "Fail";
        public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
        {
            ctx.Error(step, "failed", "boom");
            throw new InvalidOperationException("Step failed");
        }
    }

    private sealed class InMemoryRunLogStore : IWorkflowRunLogStore
    {
        public List<WorkflowRunLogEntry> Entries { get; } = new();
        public void Append(WorkflowRunLogEntry entry) => Entries.Add(entry);
        public IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200) => Entries.TakeLast(take).ToList().AsReadOnly();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        condition().Should().BeTrue("expected condition to become true within timeout");
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartContinuousAsync_ShouldReturnRunId()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(200) };
        var runtime = CreateRuntime(handler);

        var runId = await runtime.StartContinuousAsync(wf);

        runId.Should().NotBeEmpty();
        runtime.ActiveRunId.Should().Be(runId);
        runtime.ActiveWorkflowId.Should().Be(wf.Id);
    }

    [Fact]
    public async Task StartStepAsync_ShouldStartInPausedState()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null));
        var runtime = CreateRuntime(new TestStepHandler());

        await runtime.StartStepAsync(wf);

        runtime.State.Should().Be(WorkflowRunState.Paused);
    }

    [Fact]
    public async Task StartStepAsync_ShouldExecuteOneStepThenPause()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null));
        var handler = new TestStepHandler();
        var runtime = CreateRuntime(handler);

        await runtime.StartStepAsync(wf);

        handler.ExecutedSteps.Should().ContainSingle().Which.Should().Be("A");
    }

    [Fact]
    public async Task StepOnceAsync_ShouldAdvanceToNextStep()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null));
        var handler = new TestStepHandler();
        var runtime = CreateRuntime(handler);

        await runtime.StartStepAsync(wf);
        await runtime.StepOnceAsync();

        handler.ExecutedSteps.Should().HaveCount(2);
        handler.ExecutedSteps.Should().ContainInOrder("A", "B");
    }

    [Fact]
    public async Task StepOnceAsync_ShouldCompleteWhenNoMoreSteps()
    {
        var wf = MakeWorkflow(("A", "Action", null));
        var runtime = CreateRuntime(new TestStepHandler());

        await runtime.StartStepAsync(wf);

        runtime.State.Should().Be(WorkflowRunState.Completed);
    }

    [Fact]
    public async Task Cancel_ShouldSetCanceledState()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(200) };
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);
        await Task.Delay(50);
        runtime.Cancel();

        await WaitUntil(() => runtime.State is WorkflowRunState.Canceled or WorkflowRunState.Completed);
        runtime.State.Should().Be(WorkflowRunState.Canceled);
    }

    [Fact]
    public async Task StartContinuousAsync_ShouldThrow_WhenAlreadyRunning()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(200) };
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);

        var act = () => runtime.StartContinuousAsync(wf);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EmptyWorkflow_ShouldCompleteImmediately()
    {
        var wf = new WorkflowDefinition { Id = Guid.NewGuid(), Name = "Empty", Steps = new() };
        var runtime = CreateRuntime(new TestStepHandler());

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Completed);
    }

    [Fact]
    public async Task NoHandler_ShouldFailWorkflow()
    {
        var wf = MakeWorkflow(("A", "UnknownKind", null));
        var runtime = CreateRuntime(new TestStepHandler());

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Failed);
    }

    [Fact]
    public async Task SingleStepWorkflow_ShouldComplete()
    {
        var wf = MakeWorkflow(("Only", "Action", null));
        var handler = new TestStepHandler();
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Completed);

        handler.ExecutedSteps.Should().ContainSingle();
    }

    [Fact]
    public async Task LinearWorkflow_ShouldExecuteInOrder()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(50) };
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Completed);

        handler.ExecutedSteps.Should().HaveCount(3);
        handler.ExecutedSteps.Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public async Task CurrentStepId_ShouldTrackExecution()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null));
        var runtime = CreateRuntime(new TestStepHandler());

        await runtime.StartStepAsync(wf);

        runtime.CurrentStepId.Should().Be(wf.Steps[0].Id);
    }

    [Fact]
    public async Task ChangedEvent_ShouldFireOnStateChanges()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(100) };
        var runtime = CreateRuntime(handler);
        var changeCount = 0;
        runtime.Changed += () => Interlocked.Increment(ref changeCount);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Completed);

        changeCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunLog_ShouldContainEntries()
    {
        var wf = MakeWorkflow(("A", "Action", null));
        var handler = new TestStepHandler();
        var logStore = new InMemoryRunLogStore();
        var runtime = new WorkflowRuntimeService(new[] { handler }, logStore);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => runtime.State == WorkflowRunState.Completed);

        logStore.Entries.Should().NotBeEmpty();
        logStore.Entries.Should().Contain(e => e.Message.Contains("started"));
        logStore.Entries.Should().Contain(e => e.Message.Contains("completed"));
    }

    [Fact]
    public async Task Pause_ShouldTransitionToPaused()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(200) };
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => handler.ExecutedSteps.Length >= 1);
        runtime.Pause();

        runtime.State.Should().Be(WorkflowRunState.Paused);
    }

    [Fact]
    public async Task Resume_ShouldTransitionBackToRunning()
    {
        var wf = MakeWorkflow(("A", "Action", null), ("B", "Action", null), ("C", "Action", null));
        var handler = new TestStepHandler { OnExecute = async (_, _, _) => await Task.Delay(200) };
        var runtime = CreateRuntime(handler);

        await runtime.StartContinuousAsync(wf);
        await WaitUntil(() => handler.ExecutedSteps.Length >= 1);
        runtime.Pause();
        runtime.Resume();

        runtime.State.Should().Be(WorkflowRunState.Running);
    }
}
