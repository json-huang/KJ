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
        RefreshCommand = new DelegateCommand(Refresh);
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => Refresh());
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (var e in _store.GetRecent(200))
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

