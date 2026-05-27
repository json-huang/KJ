namespace KJ.Workflows.Modules;

public interface IWorkflowStepModuleCatalog
{
    IReadOnlyList<IWorkflowStepModule> GetAll();

    IWorkflowStepModule? GetModule(string kind);
}
