using Prism.Navigation.Regions;
using KJ.Modules.Core.Modules;
using Prism.Ioc;

namespace KJ.Modules.Auth;

/// <summary>
/// 认证相关 Prism 模块：会话与导航契约（<see cref="ISessionState"/> / <see cref="INavigator"/>）由主程序注册；
/// 模块内页面与后续服务应依赖 <see cref="IAuthenticationContext"/> 与持久化层接口，而非直接引用 Shell 视图。
/// </summary>
public sealed class AuthModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        // 实现类（SessionState、FrameNavigator、AuthenticationContext 等）在 KJ.App 中注册。
    }

    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<Views.LoginView, ViewModels.LoginViewModel>("AuthLogin");
        containerRegistry.RegisterForNavigation<Views.UserManagementView, ViewModels.UserManagementViewModel>("UserManagement");
        containerRegistry.RegisterForNavigation<Views.RoleManagementView, ViewModels.RoleManagementViewModel>("RoleManagement");
    }

    protected override void RegisterRegions() =>
        ContainerProvider.Resolve<IRegionManager>()
            .RegisterViewWithRegion(KJ.Modules.Core.Regions.RegionNames.MainNavigation, () => ContainerProvider.Resolve<Views.AuthNavigationView>());

    protected override void InitializeModule()
    {
        // Navigation is handled by ShellPage (GoLogin/GoMain) and MainPage.OnLoaded.
        // Only validate that required services are registered.
        _ = ContainerProvider.Resolve<ISessionState>();
        _ = ContainerProvider.Resolve<IAuthenticationContext>();
        _ = ContainerProvider.Resolve<IShellContentNavigation>();
        _ = ContainerProvider.Resolve<IPermissionService>();
    }
}
