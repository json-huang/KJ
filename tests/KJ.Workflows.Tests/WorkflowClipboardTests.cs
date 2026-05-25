using FluentAssertions;
using KJ.Workflows;
using Xunit;

namespace KJ.Workflows.Tests;

public class WorkflowClipboardTests
{
    private static WorkflowStep MakeStep(string title, double x = 0, double y = 0) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Kind = "Action",
        X = x,
        Y = y,
        Parameters = { ["key"] = "value" },
    };

    [Fact]
    public void CopySteps_ShouldStoreSteps()
    {
        var svc = new WorkflowClipboardService();
        var steps = new List<WorkflowStep> { MakeStep("A"), MakeStep("B") };

        svc.CopySteps(steps);

        svc.CanPaste.Should().BeTrue();
        svc.ClipboardCount.Should().Be(2);
    }

    [Fact]
    public void CopySteps_ShouldDeepCopy()
    {
        var svc = new WorkflowClipboardService();
        var stepA = MakeStep("A");
        var steps = new List<WorkflowStep> { stepA };

        svc.CopySteps(steps);

        // 修改原步骤
        stepA.Title = "Modified";
        stepA.Parameters["key"] = "changed";

        var pasted = svc.PasteSteps();
        pasted[0].Title.Should().Be("A");
        pasted[0].Parameters["key"].Should().Be("value");
    }

    [Fact]
    public void PasteSteps_ShouldGenerateNewIds()
    {
        var svc = new WorkflowClipboardService();
        var original = MakeStep("A");
        svc.CopySteps(new List<WorkflowStep> { original });

        var pasted = svc.PasteSteps();

        pasted.Should().HaveCount(1);
        pasted[0].Id.Should().NotBe(original.Id);
        pasted[0].Title.Should().Be("A");
    }

    [Fact]
    public void PasteSteps_ShouldOffsetPosition()
    {
        var svc = new WorkflowClipboardService();
        var step = MakeStep("A", x: 100, y: 200);
        svc.CopySteps(new List<WorkflowStep> { step });

        var pasted = svc.PasteSteps(offsetX: 30, offsetY: 50);

        pasted[0].X.Should().Be(130);
        pasted[0].Y.Should().Be(250);
    }

    [Fact]
    public void PasteSteps_ShouldUseDefaultOffset()
    {
        var svc = new WorkflowClipboardService();
        var step = MakeStep("A", x: 100, y: 200);
        svc.CopySteps(new List<WorkflowStep> { step });

        var pasted = svc.PasteSteps();

        pasted[0].X.Should().Be(120);
        pasted[0].Y.Should().Be(220);
    }

    [Fact]
    public void PasteSteps_ShouldRemapInternalReferences()
    {
        var svc = new WorkflowClipboardService();
        var stepA = MakeStep("A");
        var stepB = MakeStep("B");
        stepA.NextStepId = stepB.Id;

        svc.CopySteps(new List<WorkflowStep> { stepA, stepB });

        var pasted = svc.PasteSteps();

        // stepA 的 NextStepId 应指向新 stepB 的 ID
        pasted[0].NextStepId.Should().Be(pasted[1].Id);
    }

    [Fact]
    public void PasteSteps_ShouldRemapBranchReferences()
    {
        var svc = new WorkflowClipboardService();
        var stepA = MakeStep("Decision");
        stepA.Kind = "Decision";
        var stepB = MakeStep("Branch Target");
        stepA.Branches.Add(new WorkflowBranch
        {
            Label = "Yes",
            NextStepId = stepB.Id,
            Condition = "x > 0",
        });

        svc.CopySteps(new List<WorkflowStep> { stepA, stepB });

        var pasted = svc.PasteSteps();

        pasted[0].Branches[0].NextStepId.Should().Be(pasted[1].Id);
    }

    [Fact]
    public void CanPaste_ShouldBeFalse_WhenEmpty()
    {
        var svc = new WorkflowClipboardService();
        svc.CanPaste.Should().BeFalse();
    }

    [Fact]
    public void CanPaste_ShouldBeFalse_AfterClear()
    {
        var svc = new WorkflowClipboardService();
        svc.CopySteps(new List<WorkflowStep> { MakeStep("A") });
        svc.CanPaste.Should().BeTrue();

        svc.Clear();

        svc.CanPaste.Should().BeFalse();
        svc.ClipboardCount.Should().Be(0);
    }

    [Fact]
    public void PasteSteps_ShouldReturnEmpty_WhenNoClipboard()
    {
        var svc = new WorkflowClipboardService();
        var pasted = svc.PasteSteps();
        pasted.Should().BeEmpty();
    }

    [Fact]
    public void CopySteps_ShouldIgnoreNull()
    {
        var svc = new WorkflowClipboardService();
        svc.CopySteps(null!);
        svc.CanPaste.Should().BeFalse();
    }

    [Fact]
    public void CopySteps_ShouldIgnoreEmpty()
    {
        var svc = new WorkflowClipboardService();
        svc.CopySteps(new List<WorkflowStep>());
        svc.CanPaste.Should().BeFalse();
    }

    [Fact]
    public void PasteSteps_ShouldPreserveParameters()
    {
        var svc = new WorkflowClipboardService();
        var step = MakeStep("A");
        step.Parameters["speed"] = "100";
        step.Notes = "test note";
        svc.CopySteps(new List<WorkflowStep> { step });

        var pasted = svc.PasteSteps();

        pasted[0].Parameters.Should().ContainKey("key");
        pasted[0].Parameters["key"].Should().Be("value");
        pasted[0].Parameters.Should().ContainKey("speed");
        pasted[0].Parameters["speed"].Should().Be("100");
        pasted[0].Notes.Should().Be("test note");
    }

    [Fact]
    public void PasteSteps_ShouldNotAffectOriginal_WhenPastedMultipleTimes()
    {
        var svc = new WorkflowClipboardService();
        svc.CopySteps(new List<WorkflowStep> { MakeStep("A") });

        var paste1 = svc.PasteSteps();
        var paste2 = svc.PasteSteps();

        paste1[0].Id.Should().NotBe(paste2[0].Id);
        svc.CanPaste.Should().BeTrue(); // 剪贴板仍然可用
    }
}
