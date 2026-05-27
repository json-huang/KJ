using KJ.Workflows;

namespace KJ.Modules.Monitoring.ViewModels;

/// <summary>流程编辑器撤销/重做栈（深拷贝快照）。</summary>
internal sealed class WorkflowEditorHistory
{
    private const int MaxDepth = 50;
    private readonly Stack<WorkflowDefinition> _undo = new();
    private readonly Stack<WorkflowDefinition> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Push(WorkflowDefinition snapshot)
    {
        _redo.Clear();
        _undo.Push(WorkflowEditorSnapshot.Clone(snapshot));
        TrimToMax(_undo);
    }

    /// <summary>取出上一状态；<paramref name="current"/> 会压入重做栈。</summary>
    public WorkflowDefinition? PopUndo(WorkflowDefinition current)
    {
        if (_undo.Count == 0)
            return null;

        _redo.Push(WorkflowEditorSnapshot.Clone(current));
        TrimToMax(_redo);
        return WorkflowEditorSnapshot.Clone(_undo.Pop());
    }

    /// <summary>取出重做状态；<paramref name="current"/> 会压入撤销栈。</summary>
    public WorkflowDefinition? PopRedo(WorkflowDefinition current)
    {
        if (_redo.Count == 0)
            return null;

        _undo.Push(WorkflowEditorSnapshot.Clone(current));
        TrimToMax(_undo);
        return WorkflowEditorSnapshot.Clone(_redo.Pop());
    }

    private static void TrimToMax(Stack<WorkflowDefinition> stack)
    {
        if (stack.Count <= MaxDepth)
            return;

        var items = stack.ToArray();
        stack.Clear();
        for (var i = items.Length - MaxDepth; i < items.Length; i++)
            stack.Push(items[i]);
    }
}

internal static class WorkflowEditorSnapshot
{
    public static WorkflowDefinition Clone(WorkflowDefinition source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Version = source.Version,
            UpdatedAt = source.UpdatedAt,
            Links = source.Links.Select(l => new WorkflowLink
            {
                FromStepId = l.FromStepId,
                FromPort = l.FromPort,
                ToStepId = l.ToStepId,
                ToPort = l.ToPort,
                Label = l.Label,
            }).ToList(),
            Steps = source.Steps.Select(CloneStep).ToList(),
        };

    public static WorkflowStep CloneStep(WorkflowStep source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            NextStepId = source.NextStepId,
            Notes = source.Notes,
            Parameters = new Dictionary<string, string>(source.Parameters, StringComparer.OrdinalIgnoreCase),
            Branches = source.Branches.Select(b => new WorkflowBranch
            {
                Label = b.Label,
                NextStepId = b.NextStepId,
                Condition = b.Condition,
                ConditionType = b.ConditionType,
            }).ToList(),
        };
}
