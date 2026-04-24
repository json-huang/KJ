using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Config;

public sealed class ConfigModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry) { }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.ConfigHomePage>("ConfigHome");
        containerRegistry.Register<Views.ConfigNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.ConfigNavigationView>());

    protected override void InitializeModule() { }
}

