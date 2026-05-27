using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    private void Nav_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        if (tb.Tag is not string route || string.IsNullOrWhiteSpace(route))
            return;

        _regionManager.RequestNavigate(
            KJ.Modules.Core.Regions.RegionNames.MainContent,
            new Uri(route, UriKind.Relative));
    }
}
