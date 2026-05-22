using KJ.Workflows;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowRunLogItem
{
    public string Line1 { get; init; } = string.Empty;
    public string Line2 { get; init; } = string.Empty;
}

public sealed class WorkflowRunsViewModel : BindableBase
{
    private readonly IWorkflowRunLogStore _store;

    public IList<WorkflowRunLogItem> Items { get; } = new List<WorkflowRunLogItem>();

    public DelegateCommand RefreshCommand { get; }

    public WorkflowRunsViewModel(IWorkflowRunLogStore store)
    {
        _store = store;
        RefreshCommand = new DelegateCommand(() => _ = RefreshAsync());
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => _ = RefreshAsync());
    }

    private async Task RefreshAsync()
    {
        var entries = await Task.Run(() => _store.GetRecent(200)).ConfigureAwait(true);
        Items.Clear();
        foreach (var e in entries)
        {
            Items.Add(new WorkflowRunLogItem
            {
                Line1 = $"{e.Timestamp:HH:mm:ss.fff}  {e.Kind}  {(e.Success ? "OK" : "FAIL")}",
                Line2 = e.Success ? e.Message : $"{e.Message}  {e.Error}",
            });
        }
        RaisePropertyChanged(nameof(Items));
    }
}

