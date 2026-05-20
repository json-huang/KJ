using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using KJ.Modules.Monitoring.Services;
using KJ.Modules.Monitoring.Workflows;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Monitoring;

public sealed class MonitoringModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDeviceStatusProvider, NullDeviceStatusProvider>();
        containerRegistry.RegisterSingleton<IWorkflowStore, WorkflowJsonStore>();
    }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.MonitoringHomePage>("MonitoringHome");
        containerRegistry.RegisterForNavigation<Views.DeviceListView, ViewModels.DeviceListViewModel>("DeviceList");
        containerRegistry.RegisterForNavigation<Views.TagMonitorView, ViewModels.TagMonitorViewModel>("TagMonitor");
        containerRegistry.RegisterForNavigation<Views.TrendChartView, ViewModels.TrendChartViewModel>("TrendChart");
        containerRegistry.RegisterForNavigation<Views.DashboardView, ViewModels.DashboardViewModel>("Dashboard");
        containerRegistry.RegisterForNavigation<Views.WorkflowListPage, ViewModels.WorkflowListViewModel>("WorkflowList");
        containerRegistry.RegisterForNavigation<Views.WorkflowEditorPage, ViewModels.WorkflowEditorViewModel>("WorkflowEditor");
        containerRegistry.RegisterForNavigation<Views.WorkflowRunsPage, ViewModels.WorkflowRunsViewModel>("WorkflowRuns");
        containerRegistry.RegisterForNavigation<Views.MonitoringNavigationView>("MonitoringNav");
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.MonitoringNavigationView>());

    protected override void InitializeModule() { }
}

