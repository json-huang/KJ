namespace KJ.Workflows;

/// <summary>
/// 工作流步骤复制/粘贴服务。支持深拷贝步骤列表、粘贴时生成新 ID 并偏移位置。
/// </summary>
public sealed class WorkflowClipboardService
{
    private List<WorkflowStep>? _clipboard;

    /// <summary>剪贴板是否有内容。</summary>
    public bool CanPaste => _clipboard is { Count: > 0 };

    /// <summary>剪贴板中的步骤数量。</summary>
    public int ClipboardCount => _clipboard?.Count ?? 0;

    /// <summary>
    /// 复制选中的步骤列表（深拷贝）。原始步骤的后续修改不影响剪贴板。
    /// </summary>
    public void CopySteps(IReadOnlyList<WorkflowStep> steps)
    {
        if (steps is null || steps.Count == 0)
            return;

        _clipboard = steps.Select(CloneStep).ToList();
    }

    /// <summary>
    /// 粘贴步骤。为每个步骤生成新 ID，并按指定偏移量调整 X/Y 位置。
    /// 返回新的步骤列表（可直接添加到工作流定义中）。
    /// </summary>
    /// <param name="offsetX">X 方向偏移量（像素）。默认 20。</param>
    /// <param name="offsetY">Y 方向偏移量（像素）。默认 20。</param>
    public List<WorkflowStep> PasteSteps(double offsetX = 20, double offsetY = 20)
    {
        if (_clipboard is null || _clipboard.Count == 0)
            return [];

        // 建立旧 ID → 新 ID 的映射，用于修复内部引用
        var idMapping = new Dictionary<Guid, Guid>();

        var result = new List<WorkflowStep>(_clipboard.Count);

        foreach (var source in _clipboard)
        {
            var newId = Guid.NewGuid();
            idMapping[source.Id] = newId;

            var clone = CloneStep(source);
            clone.Id = newId;
            clone.X += offsetX;
            clone.Y += offsetY;

            result.Add(clone);
        }

        // 修复粘贴步骤之间的 NextStepId 引用（仅修复指向剪贴板内步骤的引用）
        foreach (var step in result)
        {
            if (step.NextStepId.HasValue && idMapping.TryGetValue(step.NextStepId.Value, out var mappedNext))
                step.NextStepId = mappedNext;

            foreach (var branch in step.Branches)
            {
                if (idMapping.TryGetValue(branch.NextStepId, out var mappedBranch))
                    branch.NextStepId = mappedBranch;
            }
        }

        return result;
    }

    /// <summary>清空剪贴板。</summary>
    public void Clear()
    {
        _clipboard = null;
    }

    /// <summary>深拷贝单个步骤。</summary>
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
