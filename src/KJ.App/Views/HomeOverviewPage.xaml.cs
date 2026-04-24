using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Views;

public sealed partial class HomeOverviewPage : Page
{
    public HomeOverviewPage(ViewModels.HomeOverviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
