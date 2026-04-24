using KJ.Modules.Auth;
using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.App.Services;

/// <summary>
/// 使用 Prism IRegionManager 在 Shell 内嵌区域中导航；
/// 视图需通过 RegisterForNavigation 注册且名称与 ShellRoutes 映射一致。
/// </summary>
public sealed class ShellRegionNavigationAdapter : IShellContentNavigation
{
    private readonly IRegionManager _regionManager;

    public ShellRegionNavigationAdapter(IRegionManager regionManager) => _regionManager = regionManager;

    public void Attach(ContentControl moduleContentHost)
    {
        RegionManager.SetRegionName(moduleContentHost, KJ.Modules.Core.Regions.RegionNames.MainContent);
        RegionManager.SetRegionManager(moduleContentHost, _regionManager);
        RegionManager.UpdateRegions();
    }

    public void Navigate(string routeKey)
    {
        var viewName = routeKey switch
        {
            ShellRoutes.Home => "HomeOverview",
            ShellRoutes.Monitoring => "MonitoringHome",
            ShellRoutes.Config => "ConfigHome",
            ShellRoutes.Alarm => "AlarmHome",
            ShellRoutes.Reporting => "ReportingHome",
            _ => "HomeOverview"
        };

        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri(viewName, UriKind.Relative));
    }
}
