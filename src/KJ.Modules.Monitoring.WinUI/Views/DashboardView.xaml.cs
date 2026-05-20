using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class DashboardView : Page
{
    public DashboardView() => InitializeComponent();

    public DashboardViewModel? ViewModel => DataContext as DashboardViewModel;
}
