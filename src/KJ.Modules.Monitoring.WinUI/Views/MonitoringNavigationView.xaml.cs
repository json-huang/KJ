using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class MonitoringNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;

    public MonitoringNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
    }

    private void GoDashboard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("Dashboard", UriKind.Relative));

    private void GoDeviceList_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("DeviceList", UriKind.Relative));

    private void GoTagMonitor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("TagMonitor", UriKind.Relative));

    private void GoTrend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("TrendChart", UriKind.Relative));
}

