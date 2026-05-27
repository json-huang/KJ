using KJ.Workflows.Modules.Builtins;

namespace KJ.Workflows.Modules;

public static class WorkflowStepModuleRegistration
{
    public static IWorkflowStepModuleCatalog CreateDefaultCatalog(string? extensionsDirectory = null)
    {
        var catalog = new WorkflowStepModuleCatalog();
        catalog.RegisterRange(
        [
            new StartStepModule(),
            new PlcAdsReadStepModule(),
            new PlcAdsWriteStepModule(),
            new PlcReadStepModule(),
            new PlcWriteStepModule(),
            new DecisionStepModule(),
            new HttpStepModule(),
            new ShellStepModule(),
            new MqttStepModule(),
            new DatabaseStepModule(),
        ]);

        if (!string.IsNullOrWhiteSpace(extensionsDirectory))
        {
            foreach (var module in WorkflowStepModuleLoader.LoadFromDirectory(extensionsDirectory))
                catalog.Register(module);
        }

        return catalog;
    }
}
