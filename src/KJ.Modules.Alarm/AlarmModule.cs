using KJ.Domain.Services;
using KJ.Modules.Core.Modules;
using KJ.Modules.Core.Regions;
using Prism.Navigation.Regions;
using Prism.Ioc;

namespace KJ.Modules.Alarm;

public sealed class AlarmModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        // 注册告警通知服务
        containerRegistry.RegisterSingleton<AlarmNotificationService>();
    }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.AlarmHomePage, ViewModels.AlarmHomeViewModel>("AlarmHome");
        containerRegistry.Register<Views.AlarmNavigationView>();
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.AlarmNavigationView>());

    protected override void InitializeModule()
    {
        // 初始化告警通知服务（开始监听 AlarmRaised 事件）
        ContainerProvider.Resolve<AlarmNotificationService>();
    }
}
