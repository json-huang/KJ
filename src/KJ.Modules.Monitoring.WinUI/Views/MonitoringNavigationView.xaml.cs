using KJ.Modules.Core.UI;
using KJ.Modules.Monitoring.Workflows;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Prism.Navigation;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class MonitoringNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;
    private readonly IWorkflowContentNavigator _workflowNavigator;
    private bool _syncingFromContent;
    private bool _contentHooked;

    public MonitoringNavigationView(IRegionManager regionManager, IWorkflowContentNavigator workflowNavigator)
    {
        InitializeComponent();
        _regionManager = regionManager;
        _workflowNavigator = workflowNavigator;

        Loaded += (_, _) =>
        {
            HookContentRegionNavigation();
            TrySyncSelectedFromCurrentContent();
            if (DashboardBtn.IsChecked != true &&
                DeviceListBtn.IsChecked != true &&
                TagMonitorBtn.IsChecked != true &&
                TrendBtn.IsChecked != true &&
                WorkflowListBtn.IsChecked != true &&
                WorkflowRunsBtn.IsChecked != true &&
                PluginCenterBtn.IsChecked != true)
            {
                SetSelected(DashboardBtn);
            }
        };
    }

    private void Nav_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        if (_syncingFromContent)
            return;

        SetSelected(tb);
    }

    private void SetSelected(ToggleButton tb)
    {
        if (tb.Tag is not string route || string.IsNullOrWhiteSpace(route))
            return;

        SetSelectedVisual(tb);

        if (string.Equals(route, "WorkflowEditor", StringComparison.OrdinalIgnoreCase))
        {
            MainThread.Enqueue(() => _workflowNavigator.ShowEditor(new NavigationParameters()));
            return;
        }

        var uri = new Uri(route, UriKind.Relative);
        MainThread.Enqueue(() =>
            _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, uri));
    }

    private void SetSelectedVisual(ToggleButton tb)
    {
        foreach (var b in new[] { DashboardBtn, DeviceListBtn, TagMonitorBtn, TrendBtn, WorkflowListBtn, WorkflowRunsBtn, PluginCenterBtn })
        {
            if (!ReferenceEquals(b, tb))
                b.IsChecked = false;
        }

        tb.IsChecked = true;
    }

    private void HookContentRegionNavigation()
    {
        if (_contentHooked)
            return;

        if (!_regionManager.Regions.ContainsRegionWithName(KJ.Modules.Core.Regions.RegionNames.MainContent))
            return;

        var region = _regionManager.Regions[KJ.Modules.Core.Regions.RegionNames.MainContent];
        var nav = region.NavigationService;
        if (nav is null)
            return;

        try
        {
            nav.Navigated += (_, __) => TrySyncSelectedFromCurrentContent();
            _contentHooked = true;
        }
        catch
        {
        }
    }

    private void TrySyncSelectedFromCurrentContent()
    {
        try
        {
            if (!_regionManager.Regions.ContainsRegionWithName(KJ.Modules.Core.Regions.RegionNames.MainContent))
                return;

            var region = _regionManager.Regions[KJ.Modules.Core.Regions.RegionNames.MainContent];
            var active = region.ActiveViews?.FirstOrDefault();
            if (active is null)
                return;

            var viewName = active.GetType().Name;
            var target = viewName switch
            {
                "DashboardView" => DashboardBtn,
                "DeviceListView" => DeviceListBtn,
                "TagMonitorView" => TagMonitorBtn,
                "TrendChartView" => TrendBtn,
                "WorkflowListPage" => WorkflowListBtn,
                "WorkflowCenterPage" => WorkflowListBtn,
                "WorkflowRunsPage" => WorkflowRunsBtn,
                "PluginCenterPage" => PluginCenterBtn,
                _ => null
            };

            if (target is null)
                return;

            _syncingFromContent = true;
            SetSelectedVisual(target);
        }
        finally
        {
            _syncingFromContent = false;
        }
    }
}
