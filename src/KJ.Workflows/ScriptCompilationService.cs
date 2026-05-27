using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KJ.Workflows;

/// <summary>
/// 动态编译服务。将 C# 脚本编译为程序集，加载为工作流步骤处理器。
/// </summary>
public sealed class ScriptCompilationService
{
    private readonly List<MetadataReference> _references;

    public ScriptCompilationService()
    {
        // 基础引用
        _references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        // 添加运行时程序集引用
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
        {
            if (TryCreateMetadataReference(dll) is { } reference)
                _references.Add(reference);
        }

        // 添加 KJ.Workflows 引用（用于 IWorkflowStepHandler 等）
        _references.Add(MetadataReference.CreateFromFile(typeof(IWorkflowStepHandler).Assembly.Location));
    }

    /// <summary>
    /// 编译 C# 脚本为程序集。
    /// </summary>
    /// <param name="scriptCode">C# 脚本代码。必须包含一个实现 IWorkflowStepHandler 的类。</param>
    /// <param name="assemblyName">程序集名称（可选）。</param>
    /// <returns>编译结果。</returns>
    public CompilationResult Compile(string scriptCode, string? assemblyName = null)
    {
        assemblyName ??= $"DynamicHandler_{Guid.NewGuid():N}";

        var syntaxTree = CSharpSyntaxTree.ParseText(scriptCode, path: $"{assemblyName}.cs");

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            _references,
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

    private static MetadataReference? TryCreateMetadataReference(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            if (!reader.HasMetadata)
                return null;

            return MetadataReference.CreateFromFile(path);
        }
        catch
        {
            // Runtime directories can contain native or otherwise unreadable DLLs.
            return null;
        }
    }
}

public sealed record CompilationResult(bool Success, Assembly? Assembly, string[] Errors);
