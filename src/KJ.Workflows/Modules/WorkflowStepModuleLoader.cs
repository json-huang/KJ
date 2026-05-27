using System.Reflection;
using System.Runtime.Loader;

namespace KJ.Workflows.Modules;

/// <summary>从指定目录加载实现 <see cref="IWorkflowStepModule"/> 的程序集。</summary>
public static class WorkflowStepModuleLoader
{
    public static IReadOnlyList<IWorkflowStepModule> LoadFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var result = new List<IWorkflowStepModule>();
        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                result.AddRange(LoadFromAssemblyPath(dll));
            }
            catch
            {
                // 跳过无法加载的程序集
            }
        }

        return result;
    }

    public static IReadOnlyList<IWorkflowStepModule> LoadFromAssemblyPath(string assemblyPath)
    {
        var absolute = Path.GetFullPath(assemblyPath);
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(absolute);
        return DiscoverModules(assembly);
    }

    public static IReadOnlyList<IWorkflowStepModule> DiscoverModules(Assembly assembly)
    {
        var modules = new List<IWorkflowStepModule>();
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!typeof(IWorkflowStepModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            if (Activator.CreateInstance(type) is IWorkflowStepModule module)
                modules.Add(module);
        }

        return modules;
    }
}
