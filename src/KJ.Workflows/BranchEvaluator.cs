namespace KJ.Workflows;

/// <summary>
/// 工作流条件分支评估器。根据步骤的 Branches 配置和运行时上下文决定下一步。
/// </summary>
public static class BranchEvaluator
{
    /// <summary>
    /// 评估当前步骤的分支条件，返回下一个步骤 ID。
    /// 如果没有分支或条件都不匹配，返回 NextStepId（线性默认）。
    /// </summary>
    public static Guid? EvaluateNext(WorkflowStep currentStep, WorkflowExecutionContext ctx, IReadOnlyDictionary<string, string> runtimeVars)
    {
        // 没有分支 → 线性前进
        if (currentStep.Branches.Count == 0)
            return currentStep.NextStepId;

        // 按顺序评估分支，首个匹配的生效
        foreach (var branch in currentStep.Branches)
        {
            if (EvaluateCondition(branch, runtimeVars))
            {
                ctx.Info(currentStep, $"Branch taken: '{branch.Label}' → {branch.NextStepId}");
                return branch.NextStepId;
            }
        }

        // 所有分支都不匹配，检查是否有 Default 分支
        var defaultBranch = currentStep.Branches.FirstOrDefault(b => b.ConditionType == BranchConditionType.Default);
        if (defaultBranch is not null)
        {
            ctx.Info(currentStep, $"Default branch taken: '{defaultBranch.Label}' → {defaultBranch.NextStepId}");
            return defaultBranch.NextStepId;
        }

        // 回退到线性前进
        return currentStep.NextStepId;
    }

    private static bool EvaluateCondition(WorkflowBranch branch, IReadOnlyDictionary<string, string> vars)
    {
        return branch.ConditionType switch
        {
            BranchConditionType.Default => true,
            BranchConditionType.TagEquals => EvaluateTagComparison(branch.Condition, vars, "=="),
            BranchConditionType.TagGreaterThan => EvaluateTagComparison(branch.Condition, vars, ">"),
            BranchConditionType.TagLessThan => EvaluateTagComparison(branch.Condition, vars, "<"),
            BranchConditionType.Expression => EvaluateExpression(branch.Condition, vars),
            _ => false,
        };
    }

    /// <summary>
    /// 评估标签比较条件。格式: "key=value" 或 "key>100" 或 "key&lt;50"
    /// </summary>
    private static bool EvaluateTagComparison(string condition, IReadOnlyDictionary<string, string> vars, string op)
    {
        // 解析 "key=value" 格式
        var parts = condition.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        var key = parts[0];
        var expected = parts[1];

        if (!vars.TryGetValue(key, out var actual))
            return false;

        return op switch
        {
            "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ">" => CompareNumeric(actual, expected, (a, b) => a > b),
            "<" => CompareNumeric(actual, expected, (a, b) => a < b),
            _ => false,
        };
    }

    /// <summary>
    /// 评估表达式条件。支持格式:
    /// - "tag:temp > 100" (读取运行时变量)
    /// - "param:status == running" (读取步骤参数)
    /// </summary>
    private static bool EvaluateExpression(string expression, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        // 尝试解析 "left op right" 格式
        var tokens = expression.Split(' ', 3, StringSplitOptions.TrimEntries);
        if (tokens.Length != 3) return false;

        var leftKey = tokens[0];
        var op = tokens[1];
        var rightValue = tokens[2];

        // 解析变量值
        string? actual = null;
        if (leftKey.StartsWith("tag:"))
            vars.TryGetValue(leftKey[4..], out actual);
        else if (leftKey.StartsWith("param:"))
            vars.TryGetValue(leftKey[6..], out actual);
        else
            vars.TryGetValue(leftKey, out actual);

        if (actual is null) return false;

        return op switch
        {
            "==" or "=" => string.Equals(actual, rightValue, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actual, rightValue, StringComparison.OrdinalIgnoreCase),
            ">" => CompareNumeric(actual, rightValue, (a, b) => a > b),
            ">=" => CompareNumeric(actual, rightValue, (a, b) => a >= b),
            "<" => CompareNumeric(actual, rightValue, (a, b) => a < b),
            "<=" => CompareNumeric(actual, rightValue, (a, b) => a <= b),
            _ => false,
        };
    }

    private static bool CompareNumeric(string a, string b, Func<double, double, bool> compare)
    {
        if (double.TryParse(a, out var numA) && double.TryParse(b, out var numB))
            return compare(numA, numB);
        return false;
    }
}
