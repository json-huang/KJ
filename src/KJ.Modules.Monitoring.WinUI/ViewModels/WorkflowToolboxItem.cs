using KJ.Workflows.Modules;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowToolboxItem
{
    public WorkflowToolboxItem(IWorkflowStepModule module)
    {
        Category = module.Category;
        Kind = module.Kind;
        Title = module.DisplayName;
        Description = module.Description;
    }

    public string Category { get; }
    public string Kind { get; }
    public string Title { get; }
    public string Description { get; }
}
