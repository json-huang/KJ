using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowListPage : Page
{
    public WorkflowListPage() => InitializeComponent();

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WorkflowListViewModel vm)
            await vm.RefreshAsync();
    }
}

