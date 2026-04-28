using KJ.Modules.Monitoring.Workflows;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowListItem : BindableBase
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SubTitle { get; init; } = string.Empty;
}

public sealed class WorkflowListViewModel : BindableBase
{
    private readonly IWorkflowStore _store;
    private readonly IRegionManager _regionManager;

    public IList<WorkflowListItem> Items { get; } = new List<WorkflowListItem>();

    private WorkflowListItem? _selected;
    public WorkflowListItem? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand NewCommand { get; }
    public DelegateCommand OpenSelectedCommand { get; }

    public WorkflowListViewModel(IWorkflowStore store, IRegionManager regionManager)
    {
        _store = store;
        _regionManager = regionManager;

        RefreshCommand = new DelegateCommand(async () => await RefreshAsync());
        NewCommand = new DelegateCommand(async () => await NewAsync());
        OpenSelectedCommand = new DelegateCommand(OpenSelected);
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        var list = await _store.ListAsync().ConfigureAwait(true);
        foreach (var wf in list)
        {
            Items.Add(new WorkflowListItem
            {
                Id = wf.Id,
                Name = wf.Name,
                SubTitle = $"v{wf.Version} · {wf.UpdatedAt:yyyy-MM-dd HH:mm:ss} · {wf.Steps.Count} 步",
            });
        }

        RaisePropertyChanged(nameof(Items));
    }

    private async Task NewAsync()
    {
        var wf = new WorkflowDefinition
        {
            Name = "新流程",
            Steps = new List<WorkflowStep>
            {
                new() { Title = "开始", Kind = "Start", X = 40, Y = 40 },
                new() { Title = "ADS 读", Kind = "Plc.Ads.Read", X = 320, Y = 40 },
                new() { Title = "ADS 写", Kind = "Plc.Ads.Write", X = 600, Y = 40 },
            }
        };

        wf.Steps[0].NextStepId = wf.Steps[1].Id;
        wf.Steps[1].NextStepId = wf.Steps[2].Id;
        await _store.SaveAsync(wf).ConfigureAwait(true);
        Selected = new WorkflowListItem { Id = wf.Id, Name = wf.Name, SubTitle = $"v{wf.Version} · {wf.UpdatedAt:yyyy-MM-dd HH:mm:ss}" };
        Open(wf.Id);
    }

    private void OpenSelected()
    {
        if (Selected is null)
            return;
        Open(Selected.Id);
    }

    private void Open(Guid id)
    {
        var parameters = new NavigationParameters
        {
            { "workflowId", id.ToString("N") }
        };

        _regionManager.RequestNavigate(
            KJ.Modules.Core.Regions.RegionNames.MainContent,
            new Uri("WorkflowEditor", UriKind.Relative),
            parameters);
    }
}

