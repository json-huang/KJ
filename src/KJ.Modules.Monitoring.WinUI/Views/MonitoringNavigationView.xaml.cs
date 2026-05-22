using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class MonitoringNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;
    private bool _syncingFromContent;
    private bool _contentHooked;

    public MonitoringNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;

        Loaded += (_, _) =>
        {
            HookContentRegionNavigation();
            TrySyncSelectedFromCurrentContent();
            if (DashboardBtn.IsChecked != true &&
                DeviceListBtn.IsChecked != true &&
                TagMonitorBtn.IsChecked != true &&
                TrendBtn.IsChecked != true &&
                WorkflowListBtn.IsChecked != true &&
                WorkflowEditorBtn.IsChecked != true &&
                WorkflowRunsBtn.IsChecked != true)
            {
                SetSelected(DeviceListBtn);
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

        _regionManager.RequestNavigate(
            KJ.Modules.Core.Regions.RegionNames.MainContent,
            new Uri(route, UriKind.Relative));
    }

    private void SetSelectedVisual(ToggleButton tb)
    {
        foreach (var b in new[] { DashboardBtn, DeviceListBtn, TagMonitorBtn, TrendBtn, WorkflowListBtn, WorkflowEditorBtn, WorkflowRunsBtn })
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
                "WorkflowEditorPage" => WorkflowEditorBtn,
                "WorkflowRunsPage" => WorkflowRunsBtn,
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
