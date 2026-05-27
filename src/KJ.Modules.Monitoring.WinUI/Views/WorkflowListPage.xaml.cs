using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowListPage : Page
{
    public WorkflowListViewModel ViewModel { get; }

    public WorkflowListPage(WorkflowListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        DataContextChanged += (_, _) => NavTrace.Write($"WorkflowListPage DataContext={DataContext?.GetType().Name ?? "null"}");
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavTrace.Write("WorkflowListPage loaded");
        await ViewModel.RefreshAsync();
    }

    private void OnItemDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if ((sender as ListView)?.SelectedItem is WorkflowListItem item)
            ViewModel.OpenItem(item);
    }

    private void OnOpenItemClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Button { DataContext: WorkflowListItem item })
        {
            NavTrace.Write($"WorkflowListPage.OnOpenItemClick: {item.Id:N}");
            ViewModel.OpenItem(item);
        }
        else
        {
            NavTrace.Write("WorkflowListPage.OnOpenItemClick: DataContext not WorkflowListItem");
        }
    }
}
