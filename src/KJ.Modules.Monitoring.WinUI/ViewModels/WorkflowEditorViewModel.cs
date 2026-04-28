using System.Collections.ObjectModel;
using KJ.Modules.Monitoring.Workflows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowEditorViewModel : BindableBase, IConfirmNavigationRequest
{
    private readonly IWorkflowStore _store;
    private readonly IRegionManager _regionManager;

    private WorkflowDefinition _workflow = new();
    private readonly ObservableCollection<WorkflowStep> _steps = new();

    private readonly HashSet<WorkflowStep> _trackedSteps = new();
    private CancellationTokenSource? _autosaveCts;

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    private string _saveStatusText = "已保存";
    public string SaveStatusText
    {
        get => _saveStatusText;
        private set => SetProperty(ref _saveStatusText, value);
    }

    public XamlRoot? DialogXamlRoot { get; set; }

    public string Title => "流程编辑";
    public string SubTitle => $"{_workflow.Id:N} · v{_workflow.Version} · {_workflow.UpdatedAt:yyyy-MM-dd HH:mm:ss}";

    public ObservableCollection<WorkflowStep> Steps => _steps;

    public string CanvasDebugText
        => $"Steps={_steps.Count} | WF={_workflow.Id:N} | First=({(_steps.FirstOrDefault()?.X ?? -1):0},{(_steps.FirstOrDefault()?.Y ?? -1):0})";

    private WorkflowStep? _selectedStep;
    public WorkflowStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (!SetProperty(ref _selectedStep, value))
                return;
            RaisePropertyChanged(nameof(StepTitle));
            RaisePropertyChanged(nameof(StepKind));
            RaisePropertyChanged(nameof(AdsSymbol));
            RaisePropertyChanged(nameof(AdsDataType));
            RaisePropertyChanged(nameof(AdsValue));
            RaisePropertyChanged(nameof(AdsAmsNetId));
            RaisePropertyChanged(nameof(AdsAmsPort));
        }
    }

    public string WorkflowName
    {
        get => _workflow.Name;
        set
        {
            if (_workflow.Name == value)
                return;
            _workflow.Name = value;
            RaisePropertyChanged();
            MarkDirty();
        }
    }

    public string StepTitle
    {
        get => SelectedStep?.Title ?? string.Empty;
        set
        {
            if (SelectedStep is null)
                return;
            if (SelectedStep.Title == value)
                return;
            SelectedStep.Title = value;
            RaisePropertyChanged();
            MarkDirty();
        }
    }

    public string StepKind
    {
        get => SelectedStep?.Kind ?? string.Empty;
        set
        {
            if (SelectedStep is null)
                return;
            if (SelectedStep.Kind == value)
                return;
            SelectedStep.Kind = value;
            RaisePropertyChanged();
            MarkDirty();
        }
    }

    public string AdsAmsNetId
    {
        get => GetParam("amsNetId");
        set => SetParam("amsNetId", value);
    }

    public string AdsAmsPort
    {
        get => GetParam("amsPort");
        set => SetParam("amsPort", value);
    }

    public string AdsSymbol
    {
        get => GetParam("symbol");
        set => SetParam("symbol", value);
    }

    public string AdsDataType
    {
        get => GetParam("type");
        set => SetParam("type", value);
    }

    public string AdsValue
    {
        get => GetParam("value");
        set => SetParam("value", value);
    }

    public DelegateCommand AddStepCommand { get; }
    public DelegateCommand SaveCommand { get; }
    public DelegateCommand ConnectToSelectedCommand { get; }

    private WorkflowStep? _linkFrom;
    public string LinkHint => _linkFrom is null ? "连线：选中起点后点“设为起点”→再选终点点“连接”" : $"连线起点：{_linkFrom.Title}";
    public DelegateCommand SetLinkFromCommand { get; }

    public WorkflowEditorViewModel(IWorkflowStore store, IRegionManager regionManager)
    {
        _store = store;
        _regionManager = regionManager;
        AddStepCommand = new DelegateCommand(AddStep);
        SaveCommand = new DelegateCommand(async () => await SaveAsync());
        SetLinkFromCommand = new DelegateCommand(SetLinkFrom);
        ConnectToSelectedCommand = new DelegateCommand(ConnectToSelected);

        _steps.CollectionChanged += (_, __) => RaisePropertyChanged(nameof(CanvasDebugText));
        _steps.CollectionChanged += (_, __) => TrackStepPropertyChanges();

        // Hard fallback: ensure the canvas is never blank even when navigation
        // journals/URIs are unavailable in some region setups.
        if (_steps.Count == 0)
            CreateDefaultWorkflow();
    }

    public async Task TryLoadFromNavigationAsync()
    {
        var region = _regionManager.Regions[KJ.Modules.Core.Regions.RegionNames.MainContent];
        var parameters = region?.NavigationService?.Journal?.CurrentEntry?.Parameters;
        var idStr = parameters?["workflowId"] as string;
        if (!Guid.TryParseExact(idStr, "N", out var id))
        {
            CreateDefaultWorkflow();
            return;
        }

        var wf = await _store.LoadAsync(id).ConfigureAwait(true);
        if (wf is null)
            return;

        _workflow = wf;
        _steps.Clear();
        foreach (var s in _workflow.Steps)
            _steps.Add(s);

        SelectedStep = _steps.FirstOrDefault();
        TrackStepPropertyChanges();
        IsDirty = false;
        SaveStatusText = "已保存";
        RaisePropertyChanged(nameof(SubTitle));
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(WorkflowName));
        RaisePropertyChanged(nameof(CanvasDebugText));

        await TryRecoverAutosaveIfNeededAsync().ConfigureAwait(true);
    }

    private void CreateDefaultWorkflow()
    {
        _workflow = new WorkflowDefinition
        {
            Name = "新流程",
            Steps = new List<WorkflowStep>
            {
                new() { Title = "开始", Kind = "Start", X = 40, Y = 40 },
                new()
                {
                    Title = "ADS 读",
                    Kind = "Plc.Ads.Read",
                    X = 320,
                    Y = 40,
                    Parameters =
                    {
                        ["amsNetId"] = "",
                        ["amsPort"] = "851",
                        ["symbol"] = "MAIN.nSpeed",
                        ["type"] = "DINT",
                    }
                },
                new()
                {
                    Title = "ADS 写",
                    Kind = "Plc.Ads.Write",
                    X = 600,
                    Y = 40,
                    Parameters =
                    {
                        ["amsNetId"] = "",
                        ["amsPort"] = "851",
                        ["symbol"] = "GVL.bRun",
                        ["type"] = "BOOL",
                        ["value"] = "true",
                    }
                },
            }
        };

        _workflow.Steps[0].NextStepId = _workflow.Steps[1].Id;
        _workflow.Steps[1].NextStepId = _workflow.Steps[2].Id;

        _steps.Clear();
        foreach (var s in _workflow.Steps)
            _steps.Add(s);

        SelectedStep = _steps.FirstOrDefault();
        TrackStepPropertyChanges();
        IsDirty = false;
        SaveStatusText = "已保存";
        RaisePropertyChanged(nameof(SubTitle));
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(WorkflowName));
        RaisePropertyChanged(nameof(CanvasDebugText));
    }

    private void AddStep()
    {
        var idx = _steps.Count + 1;
        _steps.Add(new WorkflowStep { Title = $"步骤 {idx}", Kind = "Plc.Ads.Read", X = 40 + (_steps.Count * 40), Y = 180 + (_steps.Count * 10) });
        SelectedStep = _steps.Last();
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(CanvasDebugText));
        MarkDirty();
    }

    private void SetLinkFrom()
    {
        _linkFrom = SelectedStep;
        RaisePropertyChanged(nameof(LinkHint));
    }

    private void ConnectToSelected()
    {
        if (_linkFrom is null || SelectedStep is null)
            return;
        if (ReferenceEquals(_linkFrom, SelectedStep))
            return;

        _linkFrom.NextStepId = SelectedStep.Id;
        _linkFrom = null;
        RaisePropertyChanged(nameof(LinkHint));
        MarkDirty();
    }

    private async Task SaveAsync()
    {
        _workflow.Steps = _steps.ToList();
        try
        {
            IsSaving = true;
            SaveStatusText = "正在保存…";
            await _store.SaveAsync(_workflow).ConfigureAwait(true);
            await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
            IsDirty = false;
            SaveStatusText = "已保存";
            RaisePropertyChanged(nameof(SubTitle));
        }
        catch (Exception ex)
        {
            SaveStatusText = $"保存失败：{ex.Message}";
            IsDirty = true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private string GetParam(string key)
    {
        if (SelectedStep is null)
            return string.Empty;
        return SelectedStep.Parameters.TryGetValue(key, out var v) ? v : string.Empty;
    }

    private void SetParam(string key, string value)
    {
        if (SelectedStep is null)
            return;
        if (string.IsNullOrWhiteSpace(value))
            SelectedStep.Parameters.Remove(key);
        else
            SelectedStep.Parameters[key] = value;

        RaisePropertyChanged();
        MarkDirty();
    }

    public async Task BeginEditorSessionAsync()
    {
        await _store.MarkEditorSessionOpenAsync(_workflow.Id).ConfigureAwait(true);
    }

    public async Task EndEditorSessionAsync()
    {
        await _store.MarkEditorSessionClosedAsync().ConfigureAwait(true);
    }

    public void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        if (!IsDirty)
        {
            continuationCallback(true);
            return;
        }

        _ = ConfirmLeaveAsync(continuationCallback);
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var idStr = navigationContext.Parameters?["workflowId"] as string;
        if (Guid.TryParseExact(idStr, "N", out var id))
        {
            // Ensure BeginEditorSessionAsync uses the correct workflow id even before view calls it.
            _workflow.Id = id;
        }
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Do not close editor session here; navigation may be canceled by ConfirmNavigationRequest.
    }

    private async Task ConfirmLeaveAsync(Action<bool> continuationCallback)
    {
        if (DialogXamlRoot is null)
        {
            continuationCallback(true);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = DialogXamlRoot,
            Title = "有未保存的更改",
            Content = "离开前要保存吗？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await SaveAsync().ConfigureAwait(true);
            continuationCallback(!IsDirty);
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            _autosaveCts?.Cancel();
            await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
            await _store.MarkEditorSessionClosedAsync().ConfigureAwait(true);
            IsDirty = false;
            SaveStatusText = "已保存";
            continuationCallback(true);
            return;
        }

        continuationCallback(false);
    }

    private void MarkDirty()
    {
        IsDirty = true;
        if (!IsSaving)
            SaveStatusText = "● 未保存";

        ScheduleAutosave();
    }

    private void ScheduleAutosave()
    {
        _autosaveCts?.Cancel();
        _autosaveCts?.Dispose();
        _autosaveCts = new CancellationTokenSource();
        var ct = _autosaveCts.Token;

        _ = AutosaveAsync(ct);
    }

    private async Task AutosaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(800, ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();

            var snapshot = new WorkflowDefinition
            {
                Id = _workflow.Id,
                Name = _workflow.Name,
                Version = _workflow.Version,
                UpdatedAt = _workflow.UpdatedAt,
                Steps = _steps.ToList(),
            };

            IsSaving = true;
            SaveStatusText = "正在保存…";
            await _store.SaveAutosaveAsync(snapshot, ct).ConfigureAwait(true);

            if (!ct.IsCancellationRequested)
            {
                IsDirty = false;
                SaveStatusText = "已保存";
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            IsDirty = true;
            SaveStatusText = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void TrackStepPropertyChanges()
    {
        foreach (var s in _steps)
        {
            if (_trackedSteps.Contains(s))
                continue;
            _trackedSteps.Add(s);
            s.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(WorkflowStep.X) or nameof(WorkflowStep.Y) or nameof(WorkflowStep.NextStepId))
                    MarkDirty();
            };
        }
    }

    private async Task TryRecoverAutosaveIfNeededAsync()
    {
        var last = await _store.GetLastUnclosedEditorSessionWorkflowIdAsync().ConfigureAwait(true);
        if (last is null || last.Value != _workflow.Id)
            return;

        if (!await _store.HasNewerAutosaveAsync(_workflow.Id).ConfigureAwait(true))
            return;

        if (DialogXamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = DialogXamlRoot,
            Title = "发现未恢复的草稿",
            Content = "检测到上次异常退出，并且存在更新的自动保存草稿。要恢复吗？",
            PrimaryButtonText = "恢复草稿",
            SecondaryButtonText = "丢弃草稿",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var wf = await _store.LoadAutosaveAsync(_workflow.Id).ConfigureAwait(true);
            if (wf is null)
                return;

            _workflow = wf;
            _steps.Clear();
            foreach (var s in _workflow.Steps)
                _steps.Add(s);

            SelectedStep = _steps.FirstOrDefault();
            TrackStepPropertyChanges();
            IsDirty = false;
            SaveStatusText = "已保存（草稿已恢复）";
            RaisePropertyChanged(nameof(SubTitle));
            RaisePropertyChanged(nameof(Steps));
            RaisePropertyChanged(nameof(WorkflowName));
            RaisePropertyChanged(nameof(CanvasDebugText));
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
            SaveStatusText = "已保存";
        }
    }
}

