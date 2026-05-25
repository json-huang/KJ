using FluentAssertions;
using Xunit;

namespace KJ.Workflows.Tests;

public class BranchEvaluatorTests
{
    private static WorkflowStep MakeStep(Guid? next = null, params WorkflowBranch[] branches) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "TestStep",
            Kind = "Decision",
            NextStepId = next,
            Branches = branches.ToList(),
        };

    private static WorkflowBranch Branch(string label, Guid next, BranchConditionType type, string condition = "") =>
        new() { Label = label, NextStepId = next, ConditionType = type, Condition = condition };

    private static WorkflowExecutionContext FakeCtx()
    {
        var logs = new List<WorkflowRunLogEntry>();
        return new WorkflowExecutionContext(Guid.NewGuid(), e => logs.Add(e));
    }

    // ── Linear fallback ──────────────────────────────────────────────────

    [Fact]
    public void NoBranches_ShouldReturnNextStepId()
    {
        var nextId = Guid.NewGuid();
        var step = MakeStep(next: nextId);
        var ctx = FakeCtx();

        var result = BranchEvaluator.EvaluateNext(step, ctx, new Dictionary<string, string>());

        result.Should().Be(nextId);
    }

    [Fact]
    public void NoBranches_NoNextStep_ShouldReturnNull()
    {
        var step = MakeStep(next: null);
        var ctx = FakeCtx();

        var result = BranchEvaluator.EvaluateNext(step, ctx, new Dictionary<string, string>());

        result.Should().BeNull();
    }

    // ── Default branch ───────────────────────────────────────────────────

    [Fact]
    public void DefaultBranch_ShouldAlwaysMatch()
    {
        var defaultTarget = Guid.NewGuid();
        var step = MakeStep(null, Branch("Default", defaultTarget, BranchConditionType.Default));
        var ctx = FakeCtx();

        var result = BranchEvaluator.EvaluateNext(step, ctx, new Dictionary<string, string>());

        result.Should().Be(defaultTarget);
    }

    // ── TagEquals ────────────────────────────────────────────────────────

    [Fact]
    public void TagEquals_ShouldMatch_WhenValuesEqual()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("High", target, BranchConditionType.TagEquals, "level=high"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["level"] = "high" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(target);
    }

    [Fact]
    public void TagEquals_ShouldNotMatch_WhenValuesDiffer()
    {
        var target = Guid.NewGuid();
        var fallback = Guid.NewGuid();
        var step = MakeStep(null,
            Branch("High", target, BranchConditionType.TagEquals, "level=high"),
            Branch("Default", fallback, BranchConditionType.Default));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["level"] = "low" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(fallback);
    }

    [Fact]
    public void TagEquals_ShouldBeCaseInsensitive()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Match", target, BranchConditionType.TagEquals, "status=RUNNING"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["status"] = "running" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(target);
    }

    [Fact]
    public void TagEquals_ShouldNotMatch_WhenKeyMissing()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Match", target, BranchConditionType.TagEquals, "missing=value"));
        var ctx = FakeCtx();

        BranchEvaluator.EvaluateNext(step, ctx, new Dictionary<string, string>()).Should().BeNull();
    }

    // ── Expression ───────────────────────────────────────────────────────

    [Fact]
    public void Expression_GreaterThan_ShouldMatch()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Hot", target, BranchConditionType.Expression, "tag:temp > 100"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["temp"] = "150" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(target);
    }

    [Fact]
    public void Expression_GreaterThan_ShouldNotMatch()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Hot", target, BranchConditionType.Expression, "tag:temp > 100"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["temp"] = "50" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().BeNull();
    }

    [Fact]
    public void Expression_Equals_ShouldMatch()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Match", target, BranchConditionType.Expression, "param:status == running"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["status"] = "running" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(target);
    }

    [Fact]
    public void Expression_LessThan_ShouldMatch()
    {
        var target = Guid.NewGuid();
        var step = MakeStep(null, Branch("Low", target, BranchConditionType.Expression, "tag:pressure < 50"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["pressure"] = "30" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(target);
    }

    // ── Branch ordering ──────────────────────────────────────────────────

    [Fact]
    public void MultipleBranches_ShouldReturnFirstMatch()
    {
        var highTarget = Guid.NewGuid();
        var medTarget = Guid.NewGuid();
        var step = MakeStep(null,
            Branch("High", highTarget, BranchConditionType.Expression, "tag:val > 100"),
            Branch("Medium", medTarget, BranchConditionType.Expression, "tag:val > 50"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["val"] = "150" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(highTarget);
    }

    [Fact]
    public void MultipleBranches_ShouldSkipNonMatching()
    {
        var highTarget = Guid.NewGuid();
        var medTarget = Guid.NewGuid();
        var step = MakeStep(null,
            Branch("High", highTarget, BranchConditionType.Expression, "tag:val > 100"),
            Branch("Medium", medTarget, BranchConditionType.Expression, "tag:val > 50"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["val"] = "75" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(medTarget);
    }

    [Fact]
    public void NoMatchingBranch_ShouldFallbackToNextStepId()
    {
        var linearTarget = Guid.NewGuid();
        var step = MakeStep(linearTarget,
            Branch("High", Guid.NewGuid(), BranchConditionType.Expression, "tag:val > 100"));
        var ctx = FakeCtx();
        var vars = new Dictionary<string, string> { ["val"] = "10" };

        BranchEvaluator.EvaluateNext(step, ctx, vars).Should().Be(linearTarget);
    }
}
