using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml;
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

    private async void OnDeleteItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Controls.Button { DataContext: WorkflowListItem item })
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除流程",
            Content = $"确定要删除流程「{item.Name}」吗？此操作会删除本地文件，无法恢复。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await ViewModel.DeleteAsync(item);
    }
}
