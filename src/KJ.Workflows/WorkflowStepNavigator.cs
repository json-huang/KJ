namespace KJ.Workflows;

/// <summary>
/// 根据流程定义解析步骤的出边（支持 Links 多连线、Decision 条件分支、线性 NextStepId）。
/// </summary>
public static class WorkflowStepNavigator
{
    /// <summary>
    /// 返回当前步骤的全部后继步骤 ID（按定义顺序）。无后继时返回空列表。
    /// </summary>
    public static IReadOnlyList<Guid> GetOutgoingStepIds(
        WorkflowDefinition workflow,
        WorkflowStep step,
        WorkflowExecutionContext ctx,
        IReadOnlyDictionary<string, string> runtimeVars)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(step);

        // 条件分支：每次只走一条
        if (IsDecisionStep(step) && step.Branches.Count > 0)
        {
            var next = BranchEvaluator.EvaluateNext(step, ctx, runtimeVars);
            return next is { } id ? [id] : [];
        }

        // 编辑器多连线
        if (workflow.Links.Count > 0)
        {
            var fromLinks = workflow.Links
                .Where(l => l.FromStepId == step.Id)
                .Select(l => l.ToStepId)
                .Distinct()
                .ToList();

            if (fromLinks.Count > 0)
                return fromLinks;
        }

        return step.NextStepId is { } linear ? [linear] : [];
    }

    public static bool IsDecisionStep(WorkflowStep step) =>
        string.Equals(step.Kind, "Decision", StringComparison.OrdinalIgnoreCase)
        || step.Branches.Count > 0;

    public static Dictionary<string, string> CollectRuntimeVars(WorkflowStep step)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in step.Parameters)
            vars[kv.Key] = kv.Value;
        return vars;
    }
}
