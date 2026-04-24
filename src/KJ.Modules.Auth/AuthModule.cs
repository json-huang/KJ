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
    }

    protected override void RegisterRegions() { }

    protected override void InitializeModule()
    {
        // 启动时校验主工程已注册会话与只读认证上下文，避免模块与 Shell 脱节。
        var session = ContainerProvider.Resolve<ISessionState>();
        _ = ContainerProvider.Resolve<IAuthenticationContext>();
        _ = ContainerProvider.Resolve<IShellContentNavigation>();
        var regionManager = ContainerProvider.Resolve<IRegionManager>();
        _ = ContainerProvider.Resolve<IPermissionService>();

        if (!session.IsSignedIn)
            regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("AuthLogin", UriKind.Relative));
        else
            regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("HomeOverview", UriKind.Relative));
    }
}
