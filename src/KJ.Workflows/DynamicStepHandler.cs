using System.Reflection;

namespace KJ.Workflows;

/// <summary>
/// 动态步骤处理器。管理编译后的脚本处理器，支持热重载。
/// </summary>
public sealed class DynamicStepHandler : IWorkflowStepHandler, IDisposable
{
    private readonly ScriptCompilationService _compiler;
    private IWorkflowStepHandler? _inner;
    private Assembly? _assembly;
    private string? _currentScript;

    public string Kind { get; private set; } = "Script";
    public string[] CompilationErrors { get; private set; } = Array.Empty<string>();
    public bool IsCompiled => _inner is not null;

    public DynamicStepHandler(ScriptCompilationService compiler)
    {
        _compiler = compiler;
    }

    /// <summary>
    /// 加载或重新加载脚本。支持热重载。
    /// </summary>
    /// <returns>编译是否成功。</returns>
    public bool LoadScript(string scriptCode, string kind = "Script")
    {
        // 脚本未变化则跳过
        if (scriptCode == _currentScript && _inner is not null)
            return true;

        var result = _compiler.Compile(scriptCode);

        if (!result.Success)
        {
            CompilationErrors = result.Errors;
            return false;
        }

        var handler = _compiler.FindHandler(result.Assembly!);
        if (handler is null)
        {
            CompilationErrors = new[] { "No IWorkflowStepHandler implementation found in script." };
            return false;
        }

        // 卸载旧的
        (_inner as IDisposable)?.Dispose();

        _inner = handler;
        _assembly = result.Assembly;
        _currentScript = scriptCode;
        Kind = kind;
        CompilationErrors = Array.Empty<string>();
        return true;
    }

    public bool CanHandle(string kind) =>
        _inner is not null && string.Equals(kind, Kind, StringComparison.OrdinalIgnoreCase);

    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        if (_inner is null)
            throw new InvalidOperationException("No script loaded. Call LoadScript first.");

        return _inner.ExecuteAsync(step, ctx, ct);
    }

    public void Dispose()
    {
        (_inner as IDisposable)?.Dispose();
        _inner = null;
    }
}
