using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KJ.Workflows;

/// <summary>
/// 动态编译服务。将 C# 脚本编译为程序集，加载为工作流步骤处理器。
/// </summary>
public sealed class ScriptCompilationService
{
    /// <summary>
    /// 编译 C# 脚本为程序集。
    /// </summary>
    /// <param name="scriptCode">C# 脚本代码。必须包含一个实现 IWorkflowStepHandler 的类。</param>
    /// <param name="assemblyName">程序集名称（可选）。</param>
    /// <returns>编译结果。</returns>
    public CompilationResult Compile(string scriptCode, string? assemblyName = null)
    {
        return Compile(scriptCode, additionalReferences: null, assemblyName);
    }

    /// <summary>
    /// 编译 C# 脚本为程序集，并追加用户提供的引用（程序集名或 dll 路径）。
    /// </summary>
    public CompilationResult Compile(
        string scriptCode,
        IEnumerable<string>? additionalReferences,
        string? assemblyName = null)
    {
        assemblyName ??= $"DynamicHandler_{Guid.NewGuid():N}";

        var syntaxTree = CSharpSyntaxTree.ParseText(scriptCode, path: $"{assemblyName}.cs");
        var refs = ScriptReferenceBuilder.Build(additionalReferences);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToArray();
            return new CompilationResult(false, null, errors);
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
        return new CompilationResult(true, assembly, Array.Empty<string>());
    }

    /// <summary>
    /// 从编译后的程序集中查找 IWorkflowStepHandler 实现。
    /// </summary>
    public IWorkflowStepHandler? FindHandler(Assembly assembly)
    {
        var handlerType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IWorkflowStepHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (handlerType is null)
            return null;

        return Activator.CreateInstance(handlerType) as IWorkflowStepHandler;
    }
}

public sealed record CompilationResult(bool Success, Assembly? Assembly, string[] Errors);
