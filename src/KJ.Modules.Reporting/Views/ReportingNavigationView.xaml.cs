using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.Modules.Reporting.Views;

public sealed partial class ReportingNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;

    public ReportingNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
    }

    private void GoHome_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("ReportingHome", UriKind.Relative));
}

