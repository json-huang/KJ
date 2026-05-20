using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Alarm;

public sealed class AlarmModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry) { }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.AlarmHomePage, ViewModels.AlarmHomeViewModel>("AlarmHome");
        containerRegistry.Register<Views.AlarmNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.AlarmNavigationView>());

    protected override void InitializeModule() { }
}

