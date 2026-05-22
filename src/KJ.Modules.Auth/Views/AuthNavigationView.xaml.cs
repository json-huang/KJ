using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.Modules.Auth.Views;

public sealed partial class AuthNavigationView : UserControl
{
    private readonly IRegionManager _regionManager;

    public AuthNavigationView(IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
    }

    private void UserMgmt_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("UserManagement", UriKind.Relative));

    private void RoleMgmt_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("RoleManagement", UriKind.Relative));
}
