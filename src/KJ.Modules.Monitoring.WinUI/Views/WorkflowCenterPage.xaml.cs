using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Monitoring.ViewModels;
using KJ.Modules.Monitoring.Workflows;
using KJ.Workflows;
using KJ.Workflows.Modules;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Prism.Ioc;
using Prism.Navigation;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowCenterPage : Page
{
    private static WorkflowCenterPage? _active;

    private readonly IWorkflowStore _store;
    private readonly IWorkflowStepModuleCatalog _moduleCatalog;

    private WorkflowEditorPage? _editorPage;
    private WorkflowEditorViewModel? _editorVm;

    public WorkflowCenterViewModel ViewModel { get; }

    public WorkflowCenterPage(
        WorkflowListViewModel listViewModel,
        IWorkflowStore store,
        IWorkflowStepModuleCatalog moduleCatalog)
    {
        _store = store;
        _moduleCatalog = moduleCatalog;
        ViewModel = new WorkflowCenterViewModel(listViewModel);
        InitializeComponent();
        DataContext = ViewModel;
        DataContextChanged += (_, _) => NavTrace.Write($"WorkflowCenterPage DataContext={DataContext?.GetType().Name ?? "null"}");
    }

    public static bool TryOpenFromAnywhere(INavigationParameters parameters)
    {
        if (_active is null)
            return false;

        _active.OpenEditor(parameters);
        return true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _active = this;
        NavTrace.Write("WorkflowCenterPage loaded");
        await ViewModel.List.RefreshAsync();

        // If someone navigated here via WorkflowNavigationBridge.
        var pending = WorkflowNavigationBridge.TakePending();
        if (pending is not null)
            OpenEditor(pending);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_active, this))
            _active = null;
        DisposeEditor();
    }

    private void OnItemDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if ((sender as ListView)?.SelectedItem is WorkflowListItem item)
            ViewModel.List.OpenItem(item);
    }

    private void OnOpenItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WorkflowListItem item })
        {
            NavTrace.Write($"WorkflowCenterPage.OnOpenItemClick: {item.Id:N}");
            ViewModel.List.OpenItem(item);
        }
    }

    private void OnBackToListClick(object sender, RoutedEventArgs e) => CloseEditor();

    private void OpenEditor(INavigationParameters parameters)
    {
        if (!parameters.ContainsKey("workflowId"))
        {
            // Open empty editor (new workflow) case
            var p = new NavigationParameters();
            foreach (var kv in parameters)
                p.Add(kv.Key, kv.Value);
            p.Add("bypassConfirm", true);
            parameters = p;
        }

        ViewModel.IsEditorVisible = true;
        ViewModel.EditorStatusText = "正在加载流程编辑器…";
        EditorPlaceholder.Visibility = Visibility.Collapsed;
        ListColumn.Width = new GridLength(0);
        ListPane.Visibility = Visibility.Collapsed;

        DisposeEditor();

        _editorVm = new WorkflowEditorViewModel(_store, _moduleCatalog)
        {
            DialogXamlRoot = XamlRoot,
        };
        _editorPage = new WorkflowEditorPage(_editorVm);
        EditorHost.Content = _editorPage;

        // Kick load immediately (don't rely on page Loaded ordering).
        _ = _editorPage.EnsureLoadedAsync(parameters);

        ViewModel.EditorStatusText = "流程编辑";
        NavTrace.Write($"WorkflowCenterPage.OpenEditor: workflowId={(parameters["workflowId"] as string) ?? "null"}");
    }

    private void CloseEditor()
    {
        ViewModel.IsEditorVisible = false;
        ViewModel.EditorStatusText = "请选择一个流程";
        EditorHost.Content = null;
        EditorPlaceholder.Visibility = Visibility.Visible;
        ListColumn.Width = new GridLength(340);
        ListPane.Visibility = Visibility.Visible;
        DisposeEditor();
    }

    private void DisposeEditor()
    {
        try
        {
            _editorPage = null;
            _editorVm = null;
        }
        catch
        {
        }
    }
}

public sealed class WorkflowCenterViewModel : Prism.Mvvm.BindableBase
{
    public WorkflowListViewModel List { get; }

    private bool _isEditorVisible;
    public bool IsEditorVisible
    {
        get => _isEditorVisible;
        set => SetProperty(ref _isEditorVisible, value);
    }

    private string _editorStatusText = "请选择一个流程";
    public string EditorStatusText
    {
        get => _editorStatusText;
        set => SetProperty(ref _editorStatusText, value);
    }

    public WorkflowCenterViewModel(WorkflowListViewModel list) => List = list;
}

