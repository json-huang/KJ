using KJ.Domain.Services;
using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Config;

public sealed class ConfigModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<TagManager>();
    }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.ConfigHomePage, ViewModels.ConfigHomeViewModel>("ConfigHome");
        containerRegistry.Register<Views.ConfigNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.ConfigNavigationView>());

    protected override void InitializeModule()
    {
        // 从持久化层加载标签配置
        var tagManager = ContainerProvider.Resolve<TagManager>();
        var tagConfigStore = ContainerProvider.Resolve<Domain.ITagConfigStore>();
        tagManager.LoadFromStore(tagConfigStore);
    }
}
