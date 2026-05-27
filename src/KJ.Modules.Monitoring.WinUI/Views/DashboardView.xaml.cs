using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class DashboardView : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardView(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshAsync();
}
