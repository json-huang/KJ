using KJ.Workflows;

namespace KJ.Workflows;

/// <summary>
/// 工作流编辑撤销/重做服务。
/// </summary>
public sealed class WorkflowUndoRedoService
{
    private readonly Stack<WorkflowSnapshot> _undoStack = new();
    private readonly Stack<WorkflowSnapshot> _redoStack = new();
    private readonly int _maxHistory;

    public bool CanUndo => _undoStack.Count >= 2;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public WorkflowUndoRedoService(int maxHistory = 50)
    {
        _maxHistory = maxHistory;
    }

    /// <summary>保存当前状态（在每次编辑操作前调用）。</summary>
    public void SaveState(List<WorkflowStep> steps, string operationName = "")
    {
        var snapshot = new WorkflowSnapshot
        {
            Steps = steps.Select(CloneStep).ToList(),
            OperationName = operationName,
            Timestamp = DateTimeOffset.Now,
        };

        _undoStack.Push(snapshot);
        _redoStack.Clear(); // 新操作清空重做栈

        // 限制历史大小
        while (_undoStack.Count > _maxHistory)
        {
            var temp = new Stack<WorkflowSnapshot>(_undoStack.Reverse().Skip(1));
            _undoStack.Clear();
            foreach (var s in temp) _undoStack.Push(s);
        }
    }

    /// <summary>撤销。返回恢复的步骤列表。</summary>
    public List<WorkflowStep>? Undo()
    {
        if (_undoStack.Count < 2) return null;

        // 弹出当前状态到重做栈
        var current = _undoStack.Pop();
        _redoStack.Push(current);

        // 返回上一个状态
        var previous = _undoStack.Peek();
        return previous.Steps.Select(CloneStep).ToList();
    }

    /// <summary>重做。返回恢复的步骤列表。</summary>
    public List<WorkflowStep>? Redo()
    {
        if (!CanRedo) return null;

        var snapshot = _redoStack.Pop();
        _undoStack.Push(snapshot);
        return snapshot.Steps.Select(CloneStep).ToList();
    }

    /// <summary>清空历史。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private static WorkflowStep CloneStep(WorkflowStep source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Kind = source.Kind,
        X = source.X,
        Y = source.Y,
        NextStepId = source.NextStepId,
        Parameters = new Dictionary<string, string>(source.Parameters, StringComparer.OrdinalIgnoreCase),
        Notes = source.Notes,
        Branches = source.Branches.Select(b => new WorkflowBranch
        {
            Label = b.Label,
            NextStepId = b.NextStepId,
            Condition = b.Condition,
            ConditionType = b.ConditionType,
        }).ToList(),
    };
}

public sealed class WorkflowSnapshot
{
    public List<WorkflowStep> Steps { get; set; } = new();
    public string OperationName { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
}
