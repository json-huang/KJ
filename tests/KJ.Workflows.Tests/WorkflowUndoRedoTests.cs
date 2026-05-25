using FluentAssertions;
using KJ.Workflows;
using Xunit;

namespace KJ.Workflows.Tests;

public class WorkflowUndoRedoTests
{
    private static List<WorkflowStep> MakeSteps(params string[] titles) =>
        titles.Select(t => new WorkflowStep { Id = Guid.NewGuid(), Title = t, Kind = "Action" }).ToList();

    [Fact]
    public void CanUndo_ShouldBeFalse_WhenEmpty()
    {
        var svc = new WorkflowUndoRedoService();
        svc.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void CanRedo_ShouldBeFalse_WhenEmpty()
    {
        var svc = new WorkflowUndoRedoService();
        svc.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void SaveState_ShouldEnableUndo_AfterTwoSaves()
    {
        var svc = new WorkflowUndoRedoService();
        var steps = MakeSteps("A", "B");

        svc.SaveState(steps, "initial");
        svc.CanUndo.Should().BeFalse(); // 只有一个状态，无法撤销

        svc.SaveState(MakeSteps("A", "B", "C"), "add C");
        svc.CanUndo.Should().BeTrue(); // 两个状态，可以撤销
        svc.UndoCount.Should().Be(2);
    }

    [Fact]
    public void Undo_ShouldReturnPreviousState()
    {
        var svc = new WorkflowUndoRedoService();
        var steps1 = MakeSteps("A");
        var steps2 = MakeSteps("A", "B");

        svc.SaveState(steps1, "add A");
        svc.SaveState(steps2, "add B");

        var restored = svc.Undo();

        restored.Should().NotBeNull();
        restored!.Should().HaveCount(1);
        restored[0].Title.Should().Be("A");
    }

    [Fact]
    public void Undo_ShouldEnableRedo()
    {
        var svc = new WorkflowUndoRedoService();
        svc.SaveState(MakeSteps("A"), "initial");
        svc.SaveState(MakeSteps("A", "B"), "add B");

        svc.Undo();

        svc.CanRedo.Should().BeTrue();
        svc.RedoCount.Should().Be(1);
    }

    [Fact]
    public void Redo_ShouldReturnForwardState()
    {
        var svc = new WorkflowUndoRedoService();
        var steps1 = MakeSteps("A");
        var steps2 = MakeSteps("A", "B");

        svc.SaveState(steps1, "add A");
        svc.SaveState(steps2, "add B");
        svc.Undo();

        var restored = svc.Redo();

        restored.Should().NotBeNull();
        restored!.Should().HaveCount(2);
        restored[1].Title.Should().Be("B");
    }

    [Fact]
    public void NewOperation_ShouldClearRedoStack()
    {
        var svc = new WorkflowUndoRedoService();
        svc.SaveState(MakeSteps("A"), "add A");
        svc.SaveState(MakeSteps("A", "B"), "add B");
        svc.Undo();
        svc.CanRedo.Should().BeTrue();

        svc.SaveState(MakeSteps("A", "C"), "add C");

        svc.CanRedo.Should().BeFalse();
        svc.RedoCount.Should().Be(0);
    }

    [Fact]
    public void Undo_ShouldReturnNull_WhenEmpty()
    {
        var svc = new WorkflowUndoRedoService();
        svc.Undo().Should().BeNull();
    }

    [Fact]
    public void Redo_ShouldReturnNull_WhenEmpty()
    {
        var svc = new WorkflowUndoRedoService();
        svc.Redo().Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldEmptyBothStacks()
    {
        var svc = new WorkflowUndoRedoService();
        svc.SaveState(MakeSteps("A"), "add A");
        svc.Undo();

        svc.Clear();

        svc.CanUndo.Should().BeFalse();
        svc.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void SaveState_ShouldDeepCloneSteps()
    {
        var svc = new WorkflowUndoRedoService();
        var steps = MakeSteps("A");
        steps[0].Parameters["key"] = "value";

        svc.SaveState(steps, "initial");
        svc.SaveState(MakeSteps("A", "B"), "add B"); // 需要两个状态才能撤销

        steps[0].Parameters["key"] = "modified"; // 修改原数据

        var restored = svc.Undo();
        restored![0].Parameters["key"].Should().Be("value"); // 不受后续修改影响
    }

    [Fact]
    public void MultipleUndo_ShouldWalkBackward()
    {
        var svc = new WorkflowUndoRedoService();
        svc.SaveState(MakeSteps("A"), "step 1");
        svc.SaveState(MakeSteps("A", "B"), "step 2");
        svc.SaveState(MakeSteps("A", "B", "C"), "step 3");

        var r1 = svc.Undo(); // back to 2
        var r2 = svc.Undo(); // back to 1

        r1.Should().HaveCount(2);
        r2.Should().HaveCount(1);
    }
}
