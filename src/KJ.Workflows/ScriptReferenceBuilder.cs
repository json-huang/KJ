using System.Reflection;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace KJ.Workflows;

/// <summary>构建脚本编译/智能提示共用的 MetadataReference 列表。</summary>
public static class ScriptReferenceBuilder
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> BaseReferences = new(BuildBaseReferences);

    public static IReadOnlyList<MetadataReference> GetBaseReferences() => BaseReferences.Value;

    public static IReadOnlyList<MetadataReference> Build(IEnumerable<string>? additionalReferences)
    {
        if (additionalReferences is null)
            return BaseReferences.Value;

        List<MetadataReference>? combined = null;
        foreach (var raw in additionalReferences)
        {
            var token = raw?.Trim();
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var r = ResolveReference(token);
            if (r is null)
                continue;

            combined ??= new List<MetadataReference>(BaseReferences.Value.Count + 8);
            if (combined.Count == 0)
                combined.AddRange(BaseReferences.Value);
            combined.Add(r);
        }

        return combined ?? BaseReferences.Value;
    }

    public static IEnumerable<string> ParseReferenceLines(string? referencesText) =>
        (referencesText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

    private static List<MetadataReference> BuildBaseReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IWorkflowStepHandler).Assembly.Location),
        };

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
        {
            if (TryCreateMetadataReference(dll) is { } reference)
                references.Add(reference);
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (asm.IsDynamic)
                    continue;

                var loc = asm.Location;
                if (string.IsNullOrWhiteSpace(loc) || !File.Exists(loc))
                    continue;

                if (TryCreateMetadataReference(loc) is { } reference)
                    references.Add(reference);
            }
            catch
            {
                // ignored
            }
        }

        return references;
    }

    internal static MetadataReference? TryCreateMetadataReference(string path)
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
            return null;
        }
    }

    internal static MetadataReference? ResolveReference(string token)
    {
        try
        {
            if (File.Exists(token))
                return TryCreateMetadataReference(token);
        }
        catch
        {
            // ignore
        }

        try
        {
            var asm = Assembly.Load(new AssemblyName(token));
            if (!string.IsNullOrWhiteSpace(asm.Location) && File.Exists(asm.Location))
                return TryCreateMetadataReference(asm.Location);
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
