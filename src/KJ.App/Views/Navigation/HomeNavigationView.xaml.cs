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

        // Avoid setting IsChecked in XAML; set after load.
        Loaded += (_, _) =>
        {
            HomeBtn.IsChecked = true;
            HomeBtn.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjNavSelectedBrush"];
        };
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
}

