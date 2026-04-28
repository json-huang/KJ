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

        // Some WinUI/Uno combinations crash when setting IsChecked in XAML.
        // Set the default selection after load instead.
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

    private void Nav_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        if (tb.IsChecked == true)
            return;

        tb.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjNavHoverBrush"];
    }

    private void Nav_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        if (tb.IsChecked == true)
            return;

        tb.Background = null;
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
        // single-select behavior
        foreach (var b in new[] { DashboardBtn, DeviceListBtn, TagMonitorBtn, TrendBtn, WorkflowListBtn, WorkflowEditorBtn, WorkflowRunsBtn })
        {
            if (!ReferenceEquals(b, tb))
            {
                b.IsChecked = false;
                b.Background = null;
            }
        }

        tb.IsChecked = true;
        tb.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjNavSelectedBrush"];
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

        // Prism.Uno: rely on navigation events when ActiveViews isn't ready at Loaded.
        // If the event isn't available in a given platform build, this will be a no-op.
        try
        {
            nav.Navigated += (_, __) => TrySyncSelectedFromCurrentContent();
            _contentHooked = true;
        }
        catch
        {
            // fall back to one-shot sync only
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

