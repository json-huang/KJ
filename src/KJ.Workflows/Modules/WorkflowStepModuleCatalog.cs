using System.Collections.Concurrent;

namespace KJ.Workflows.Modules;

public sealed class WorkflowStepModuleCatalog : IWorkflowStepModuleCatalog
{
    private readonly ConcurrentDictionary<string, IWorkflowStepModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IWorkflowStepModule module)
    {
        if (string.IsNullOrWhiteSpace(module.Kind))
            throw new ArgumentException("Module Kind is required.", nameof(module));

        _modules[module.Kind] = module;
    }

    public void RegisterRange(IEnumerable<IWorkflowStepModule> modules)
    {
        foreach (var module in modules)
            Register(module);
    }

    public IReadOnlyList<IWorkflowStepModule> GetAll() =>
        _modules.Values.OrderBy(m => m.Order).ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

    public IWorkflowStepModule? GetModule(string kind) =>
        _modules.TryGetValue(kind, out var module) ? module : null;
}
