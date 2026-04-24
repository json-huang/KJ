using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.Modules.Config.Views;

public sealed partial class ConfigNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;

    public ConfigNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
    }

    private void GoHome_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("ConfigHome", UriKind.Relative));
}

