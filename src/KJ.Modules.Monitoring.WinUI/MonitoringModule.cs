using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using KJ.Modules.Monitoring.Services;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Monitoring;

public sealed class MonitoringModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDeviceStatusProvider, NullDeviceStatusProvider>();
    }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.MonitoringHomePage>("MonitoringHome");
        containerRegistry.RegisterForNavigation<Views.DeviceListView, ViewModels.DeviceListViewModel>("DeviceList");
        containerRegistry.RegisterForNavigation<Views.TagMonitorView>("TagMonitor");
        containerRegistry.RegisterForNavigation<Views.TrendChartView>("TrendChart");
        containerRegistry.RegisterForNavigation<Views.DashboardView>("Dashboard");
        containerRegistry.RegisterForNavigation<Views.MonitoringNavigationView>("MonitoringNav");
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.MonitoringNavigationView>());

    protected override void InitializeModule() { }
}

