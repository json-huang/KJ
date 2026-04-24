using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Monitoring;

public sealed class MonitoringModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry) { }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.MonitoringHomePage>("MonitoringHome");
        containerRegistry.RegisterForNavigation<Views.DeviceListView>("DeviceList");
        containerRegistry.RegisterForNavigation<Views.TagMonitorView>("TagMonitor");
        containerRegistry.RegisterForNavigation<Views.TrendChartView>("TrendChart");
        containerRegistry.RegisterForNavigation<Views.DashboardView>("Dashboard");
        containerRegistry.Register<Views.MonitoringNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.MonitoringNavigationView>());

    protected override void InitializeModule() { }
}

