using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Reporting;

public sealed class ReportingModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry) { }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.ReportingHomePage, ViewModels.ReportingHomeViewModel>("ReportingHome");
        containerRegistry.Register<Views.ReportingNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.ReportingNavigationView>());

    protected override void InitializeModule() { }
}

