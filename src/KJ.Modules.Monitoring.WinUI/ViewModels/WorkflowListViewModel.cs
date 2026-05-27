using System.Collections.ObjectModel;
using KJ.Workflows;
using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Core.UI;
using KJ.Modules.Monitoring.Workflows;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;

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
    private readonly IWorkflowContentNavigator _contentNavigator;

    public ObservableCollection<WorkflowListItem> Items { get; } = new();

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private WorkflowListItem? _selected;
    public WorkflowListItem? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand NewCommand { get; }
    public DelegateCommand OpenSelectedCommand { get; }
    public DelegateCommand<WorkflowListItem?> OpenItemCommand { get; }

    public WorkflowListViewModel(IWorkflowStore store, IWorkflowContentNavigator contentNavigator)
    {
        _store = store;
        _contentNavigator = contentNavigator;

        RefreshCommand = new DelegateCommand(async () => await RefreshAsync());
        NewCommand = new DelegateCommand(async () => await NewAsync());
        OpenSelectedCommand = new DelegateCommand(OpenSelected);
        OpenItemCommand = new DelegateCommand<WorkflowListItem?>(OpenItem);
    }

    public void OpenItem(WorkflowListItem? item)
    {
        if (item is null)
        {
            NavTrace.Write("WorkflowList.OpenItem: item is null");
            return;
        }

        Selected = item;
        NavTrace.Write($"WorkflowList.OpenItem: id={item.Id:N}");
        Open(item.Id);
    }

    public async Task RefreshAsync()
    {
        NavTrace.Write("WorkflowList.RefreshAsync: start");
        StatusText = "加载中…";
        try
        {
            var list = await _store.ListAsync().ConfigureAwait(false);
            MainThread.Enqueue(() =>
            {
                Items.Clear();
                foreach (var wf in list)
                {
                    Items.Add(new WorkflowListItem
                    {
                        Id = wf.Id,
                        Name = wf.Name,
                        SubTitle = $"v{wf.Version} · {wf.UpdatedAt:yyyy-MM-dd HH:mm:ss} · {wf.Steps.Count} 步",
                    });
                }

                IsEmpty = Items.Count == 0;
                StatusText = IsEmpty ? "暂无流程，请点击「新建」创建" : $"共 {Items.Count} 条流程";
                NavTrace.Write($"WorkflowList.RefreshAsync: done count={Items.Count}");
            });
        }
        catch (Exception ex)
        {
            MainThread.Enqueue(() =>
            {
                IsEmpty = true;
                StatusText = $"加载失败: {ex.Message}";
            });
            NavTrace.Write($"WorkflowList.RefreshAsync: error {ex}");
        }
    }

    private async Task NewAsync()
    {
        NavTrace.Write("WorkflowList.NewAsync: start");
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
        await _store.SaveAsync(wf).ConfigureAwait(false);
        NavTrace.Write($"WorkflowList.NewAsync: saved id={wf.Id:N}");

        var item = new WorkflowListItem
        {
            Id = wf.Id,
            Name = wf.Name,
            SubTitle = $"v{wf.Version} · {wf.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
        };

        var id = wf.Id;
        // SaveAsync 后常在线程池线程；必须用主线程调度导航（WinUI 无 SyncContext）
        MainThread.Enqueue(() =>
        {
            Items.Insert(0, item);
            IsEmpty = false;
            StatusText = $"共 {Items.Count} 条流程";
            Selected = item;
            NavTrace.Write($"WorkflowList.NewAsync: enqueue navigate id={id:N}");
            Open(id, isNew: true);
        });
    }

    private void OpenSelected()
    {
        if (Selected is null)
        {
            NavTrace.Write("WorkflowList.OpenSelected: Selected is null");
            return;
        }

        NavTrace.Write($"WorkflowList.OpenSelected: id={Selected.Id:N}");
        Open(Selected.Id);
    }

    private void Open(Guid id, bool isNew = false)
    {
        var parameters = new NavigationParameters
        {
            { "workflowId", id.ToString("N") },
            { "bypassConfirm", true },
        };
        if (isNew)
            parameters.Add("isNew", true);

        MainThread.Enqueue(() => ShowEditorOnUiThread(parameters));
    }

    private bool _isNavigating;

    private void ShowEditorOnUiThread(NavigationParameters parameters)
    {
        if (_isNavigating)
        {
            NavTrace.Write("WorkflowList.ShowEditor: skipped (already navigating)");
            return;
        }

        _isNavigating = true;
        StatusText = "正在打开流程…";
        try
        {
            NavTrace.Write($"WorkflowList.ShowEditor: workflowId={parameters["workflowId"]}");
            _contentNavigator.ShowEditor(parameters);
            StatusText = $"共 {Items.Count} 条流程";
            NavTrace.Write("WorkflowList.ShowEditor: done");
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            StatusText = $"打开失败：{detail}";
            NavTrace.Write($"WorkflowList.ShowEditor: error {ex}");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}

