using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.App.Views.Navigation;

public sealed partial class HomeNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;

    public HomeNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
    }

    private void Home_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("HomeOverview", UriKind.Relative));
}

