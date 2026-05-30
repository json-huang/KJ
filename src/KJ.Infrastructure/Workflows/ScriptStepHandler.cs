using System.Collections.Concurrent;
using KJ.Workflows;
using KJ.Workflows.Modules.Builtins;

namespace KJ.Infrastructure.Workflows;

/// <summary>执行 Kind=Script 的步骤：从参数 script 动态编译并调用 IWorkflowStepHandler。</summary>
public sealed class ScriptStepHandler : IWorkflowStepHandler, IDisposable
{
    private readonly ScriptCompilationService _compiler;
    private readonly ConcurrentDictionary<string, DynamicStepHandler> _handlersByScript = new(StringComparer.Ordinal);

    public ScriptStepHandler(ScriptCompilationService compiler)
    {
        _compiler = compiler;
    }

    public bool CanHandle(string kind) =>
        string.Equals(kind, ScriptStepDefaults.Kind, StringComparison.OrdinalIgnoreCase);

    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        if (!step.Parameters.TryGetValue("script", out var script) || string.IsNullOrWhiteSpace(script))
        {
            ctx.Error(step, "脚本为空", "请在属性面板填写 script 参数。");
            throw new InvalidOperationException("Script step has no script code.");
        }

        step.Parameters.TryGetValue("references", out var refsRaw);
        var refs = string.IsNullOrWhiteSpace(refsRaw)
            ? null
            : refsRaw
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

        var handler = _handlersByScript.GetOrAdd(script, _ => new DynamicStepHandler(_compiler));

        if (!handler.LoadScript(script, refs, ScriptStepDefaults.Kind))
        {
            var detail = string.Join(Environment.NewLine, handler.CompilationErrors);
            ctx.Error(step, "脚本编译失败", detail);
            throw new InvalidOperationException($"Script compilation failed:{Environment.NewLine}{detail}");
        }

        ctx.Info(step, "脚本编译成功，开始执行…");
        return handler.ExecuteAsync(step, ctx, ct);
    }

    public void Dispose()
    {
        foreach (var handler in _handlersByScript.Values)
            handler.Dispose();
        _handlersByScript.Clear();
    }
}
