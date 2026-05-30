using System.Collections.ObjectModel;
using System.ComponentModel;
using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Core.UI;
using KJ.Workflows;
using KJ.Workflows.Modules;
using KJ.Workflows.Modules.Builtins;
using KJ.Modules.Monitoring.Workflows;
using KJ.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowEditorViewModel : BindableBase, IConfirmNavigationRequest
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowStepModuleCatalog _moduleCatalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DispatcherQueue? _dispatcher;
    private IWorkflowRuntime? _runtime;
    private readonly ScriptCompilationService _scriptCompiler = new();

    private WorkflowDefinition _workflow = new();
    private readonly ObservableCollection<WorkflowStep> _steps = new();
    public ObservableCollection<WorkflowLink> Links { get; } = new();

    private readonly HashSet<WorkflowStep> _trackedSteps = new();
    private CancellationTokenSource? _autosaveCts;
    private int _canvasInteractionDepth;
    private bool _skipRecoveryPrompt;
    private bool _explicitSavePerformed;
    private readonly List<WorkflowStep> _clipboardSteps = new();
    private int _pasteGeneration;
    private readonly ObservableCollection<WorkflowStep> _selectedSteps = new();
    private readonly WorkflowEditorHistory _history = new();
    private bool _isRestoringHistory;
    private (Guid StepId, string Field)? _undoCoalesceToken;

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

    public ObservableCollection<WorkflowToolboxItem> ToolboxItems { get; } = new();

    public ObservableCollection<WorkflowStepPropertyFieldViewModel> StepPropertyFields { get; } = new();

    public sealed record DeviceOption(string DeviceId, string Label, string DriverType);

    public ObservableCollection<DeviceOption> AvailableDevices { get; } = new();

    public IReadOnlyList<string> PlcTypeOptions { get; } =
    [
        "BOOL",
        "DINT",
        "LINT",
        "REAL",
        "LREAL",
        "STRING",
    ];

    public bool HasStepPropertyFields => StepPropertyFields.Count > 0;

    public bool IsScriptStepSelected =>
        SelectedStep is not null &&
        string.Equals(SelectedStep.Kind, ScriptStepDefaults.Kind, StringComparison.OrdinalIgnoreCase);

    private string _scriptDraft = string.Empty;
    public string ScriptDraft
    {
        get => _scriptDraft;
        set => SetProperty(ref _scriptDraft, value ?? string.Empty);
    }

    private string _scriptReferencesDraft = string.Empty;
    public string ScriptReferencesDraft
    {
        get => _scriptReferencesDraft;
        set => SetProperty(ref _scriptReferencesDraft, value ?? string.Empty);
    }

    public bool ShowNoExtraParamsHint => !HasStepPropertyFields && !IsScriptStepSelected;

    public string SelectedModuleDescription =>
        SelectedStep is null
            ? string.Empty
            : _moduleCatalog.GetModule(SelectedStep.Kind)?.Description ?? "未注册模块类型，可在步骤参数中手动维护。";

    public string CanvasDebugText
        => $"Steps={_steps.Count} | WF={_workflow.Id:N} | First=({(_steps.FirstOrDefault()?.X ?? -1):0},{(_steps.FirstOrDefault()?.Y ?? -1):0})";

    public Guid? RuntimeCurrentStepId => _runtime?.CurrentStepId;

    public string RuntimeHint
    {
        get
        {
            if (_runtime is null)
                return "未运行";

            var state = _runtime.State;
            if (state == WorkflowRunState.Idle)
                return "未运行";

            var stepId = _runtime.CurrentStepId;
            var stepTitle = stepId is null ? "" : _steps.FirstOrDefault(s => s.Id == stepId)?.Title;
            return stepTitle is null ? $"状态：{state}" : $"状态：{state} · 当前：{stepTitle}";
        }
    }

    private WorkflowStep? _selectedStep;
    public WorkflowStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (!SetProperty(ref _selectedStep, value))
                return;
            _undoCoalesceToken = null;
            RaisePropertyChanged(nameof(StepTitle));
            RaisePropertyChanged(nameof(StepKind));
            RaisePropertyChanged(nameof(SelectedModuleDescription));
            RaisePropertyChanged(nameof(IsScriptStepSelected));
            LoadScriptDraftFromSelected();
            RefreshStepPropertyFields();

            if (value is null)
            {
                if (_selectedSteps.Count > 0)
                {
                    _selectedSteps.Clear();
                    RaisePropertyChanged(nameof(SelectedSteps));
                }
            }
            else if (!_selectedSteps.Contains(value))
            {
                _selectedSteps.Clear();
                _selectedSteps.Add(value);
                RaisePropertyChanged(nameof(SelectedSteps));
            }
        }
    }

    public ObservableCollection<WorkflowStep> SelectedSteps => _selectedSteps;

    public void ClearSelection()
    {
        if (_selectedStep is null && _selectedSteps.Count == 0)
            return;

        _selectedStep = null;
        RaisePropertyChanged(nameof(SelectedStep));
        RaisePropertyChanged(nameof(StepTitle));
        RaisePropertyChanged(nameof(StepKind));
        RaisePropertyChanged(nameof(SelectedModuleDescription));
        RaisePropertyChanged(nameof(IsScriptStepSelected));
        LoadScriptDraftFromSelected();
        RefreshStepPropertyFields();

        _selectedSteps.Clear();
        RaisePropertyChanged(nameof(SelectedSteps));
    }

    public void SetSelection(IEnumerable<WorkflowStep> steps)
    {
        var list = steps.Where(s => s is not null).Distinct().ToList();
        if (list.Count == 0)
        {
            ClearSelection();
            return;
        }

        _selectedSteps.Clear();
        foreach (var s in list)
            _selectedSteps.Add(s);
        RaisePropertyChanged(nameof(SelectedSteps));

        SelectedStep = list[^1];
    }

    public void ToggleSelection(WorkflowStep step)
    {
        if (_selectedSteps.Contains(step))
        {
            _selectedSteps.Remove(step);
            RaisePropertyChanged(nameof(SelectedSteps));

            if (ReferenceEquals(_selectedStep, step))
                SelectedStep = _selectedSteps.LastOrDefault();
            return;
        }

        _selectedSteps.Add(step);
        RaisePropertyChanged(nameof(SelectedSteps));
        SelectedStep = step;
    }

    public void AddToSelection(WorkflowStep step)
    {
        if (!_selectedSteps.Contains(step))
        {
            _selectedSteps.Add(step);
            RaisePropertyChanged(nameof(SelectedSteps));
        }

        SelectedStep = step;
    }

    public string WorkflowName
    {
        get => _workflow.Name;
        set
        {
            if (_workflow.Name == value)
                return;
            RecordUndoCheckpointForField(_workflow.Id, nameof(WorkflowName));
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
            RecordUndoCheckpointForField(SelectedStep.Id, "title");
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
            RecordUndoCheckpointForField(SelectedStep.Id, "kind");
            SelectedStep.Kind = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectedModuleDescription));
            RefreshStepPropertyFields();
            MarkDirty();
        }
    }

    public DelegateCommand AddStepCommand { get; }
    public DelegateCommand<WorkflowToolboxItem> AddFromToolboxCommand { get; }
    public DelegateCommand SaveCommand { get; }
    public DelegateCommand RunCommand { get; }
    public DelegateCommand StepCommand { get; }
    public DelegateCommand PauseCommand { get; }
    public DelegateCommand ResumeCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand ClearEditorLogCommand { get; }
    public DelegateCommand ClearRunOutputCommand { get; }
    public DelegateCommand UndoCommand { get; }
    public DelegateCommand RedoCommand { get; }
    public DelegateCommand SaveScriptCommand { get; }
    public DelegateCommand CompileScriptCommand { get; }
    public DelegateCommand BrowseDllReferencesCommand { get; }

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    private const int MaxEditorLogLines = 400;
    private const int MaxRunOutputLines = 300;

    private string _editorLogText = string.Empty;
    public string EditorLogText
    {
        get => _editorLogText;
        set => SetProperty(ref _editorLogText, value);
    }

    private string _runOutputText = string.Empty;
    public string RunOutputText
    {
        get => _runOutputText;
        set => SetProperty(ref _runOutputText, value);
    }

    private WorkflowStep? _linkFrom;
    private WorkflowPort _linkFromPort = WorkflowPort.Right;

    public WorkflowEditorViewModel(IWorkflowStore store, IWorkflowStepModuleCatalog moduleCatalog, IServiceScopeFactory scopeFactory)
    {
        _store = store;
        _moduleCatalog = moduleCatalog;
        _scopeFactory = scopeFactory;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        InitializeToolbox();
        AddStepCommand = new DelegateCommand(AddStep);
        AddFromToolboxCommand = new DelegateCommand<WorkflowToolboxItem>(AddStepFromToolbox);
        SaveCommand = new DelegateCommand(ExecuteSave);
        RunCommand = new DelegateCommand(async () => await RunContinuousAsync(), () => CanStartContinuous());
        StepCommand = new DelegateCommand(async () => await StepAsync(), () => CanStep());
        PauseCommand = new DelegateCommand(Pause, () => _runtime?.State == WorkflowRunState.Running);
        ResumeCommand = new DelegateCommand(Resume, () => _runtime?.State == WorkflowRunState.Paused);
        CancelCommand = new DelegateCommand(Cancel, () => _runtime?.State is WorkflowRunState.Running or WorkflowRunState.Paused);
        ClearEditorLogCommand = new DelegateCommand(ClearEditorLog);
        ClearRunOutputCommand = new DelegateCommand(ClearRunOutput);
        UndoCommand = new DelegateCommand(Undo, () => CanUndo);
        RedoCommand = new DelegateCommand(Redo, () => CanRedo);
        SaveScriptCommand = new DelegateCommand(ExecuteSaveScript, () => IsScriptStepSelected);
        CompileScriptCommand = new DelegateCommand(CompileScriptDraft, () => IsScriptStepSelected);
        BrowseDllReferencesCommand = new DelegateCommand(async () => await BrowseDllReferencesAsync(), () => IsScriptStepSelected);

        _steps.CollectionChanged += (_, __) => TrackStepPropertyChanges();
        AppendEditorLog("流程编辑器已就绪。");
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcher is null)
        {
            action();
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _ = _dispatcher.TryEnqueue(() => action());
    }

    private async void RefreshAvailableDevicesBestEffort()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var mgr = scope.ServiceProvider.GetRequiredService<IDeviceManager>();
            var list = mgr.ListDevices()
                .OrderBy(d => d.DisplayName)
                .ToList();

            RunOnUiThread(() =>
            {
                AvailableDevices.Clear();
                foreach (var d in list)
                {
                    var hostPort = string.IsNullOrWhiteSpace(d.Host)
                        ? ""
                        : (d.Port > 0 ? $"{d.Host}:{d.Port}" : d.Host);
                    var label = string.IsNullOrWhiteSpace(hostPort)
                        ? $"{d.DisplayName}  ·  {d.DeviceId}  ·  {d.DriverType}"
                        : $"{d.DisplayName}  ·  {d.DeviceId}  ·  {d.DriverType}  ·  {hostPort}";
                    AvailableDevices.Add(new DeviceOption(d.DeviceId, label, d.DriverType));
                }

                RaisePropertyChanged(nameof(AvailableDevices));
                TryApplyDefaultDeviceToSelectedStep();
            });
        }
        catch
        {
            // best-effort only
        }
    }

    private static bool IsPlcAdsStep(string kind) =>
        string.Equals(kind, "Plc.Ads.Read", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Plc.Ads.Write", StringComparison.OrdinalIgnoreCase);

    private void TryApplyDefaultDevice(WorkflowStep step)
    {
        if (!IsPlcAdsStep(step.Kind))
            return;

        if (!string.IsNullOrWhiteSpace(step.Parameters.GetValueOrDefault("device")))
            return;

        var device = AvailableDevices.FirstOrDefault(d =>
            d.DriverType.Contains("Beckhoff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.DriverType, "Plc.Beckhoff.Ads", StringComparison.OrdinalIgnoreCase))
            ?? AvailableDevices.FirstOrDefault();

        if (device is null)
            return;

        step.Parameters["device"] = device.DeviceId;
    }

    private void TryApplyDefaultDeviceToSelectedStep()
    {
        if (SelectedStep is null)
            return;

        if (!string.IsNullOrWhiteSpace(SelectedStep.Parameters.GetValueOrDefault("device")))
            return;

        TryApplyDefaultDevice(SelectedStep);
        RefreshStepPropertyFields();
    }

    private bool ValidatePlcAdsStepsBeforeRun(out string? error)
    {
        foreach (var step in _steps.Where(s => IsPlcAdsStep(s.Kind)))
            TryApplyDefaultDevice(step);

        foreach (var step in _steps.Where(s => IsPlcAdsStep(s.Kind)))
        {
            var deviceId = step.Parameters.GetValueOrDefault("device", "");
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                error = $"步骤「{step.Title}」未选择设备：请在属性面板的「设备」下拉框选择 Beckhoff ADS 设备。";
                return false;
            }

            var known = AvailableDevices.Any(d =>
                string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (!known)
            {
                error = $"步骤「{step.Title}」的设备 ID「{deviceId}」不存在，请先在【设备配置】中创建或刷新设备列表。";
                return false;
            }
        }

        error = null;
        return true;
    }

    private async void ExecuteSave()
    {
        try
        {
            AppendEditorLog("SaveCommand: invoked");
            await SaveAsync().ConfigureAwait(true);
            AppendEditorLog("SaveCommand: completed");
        }
        catch (Exception ex)
        {
            // DelegateCommand(async ()=>...) 的异常可能被吞；这里显式落日志
            AppendEditorLog($"SaveCommand: exception: {ex}", "ERROR");
            SaveStatusText = $"保存失败：{ex.Message}";
            IsDirty = true;
        }
    }

    public void AppendEditorLog(string message, string level = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunOnUiThread(() =>
        {
            var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [{level}] {message.Trim()}";
            EditorLogText = string.IsNullOrWhiteSpace(EditorLogText)
                ? line
                : $"{EditorLogText}{Environment.NewLine}{line}";

            var lines = EditorLogText.Split(Environment.NewLine);
            if (lines.Length > MaxEditorLogLines)
                EditorLogText = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxEditorLogLines));

            RaisePropertyChanged(nameof(EditorLogText));
        });
    }

    private void ClearEditorLog()
    {
        EditorLogText = string.Empty;
        AppendEditorLog("编辑日志已清空。");
    }

    public void AppendRunOutput(string message, string level = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunOnUiThread(() =>
        {
            var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [{level}] {message.Trim()}";
            RunOutputText = string.IsNullOrWhiteSpace(RunOutputText)
                ? line
                : $"{RunOutputText}{Environment.NewLine}{line}";

            var lines = RunOutputText.Split(Environment.NewLine);
            if (lines.Length > MaxRunOutputLines)
                RunOutputText = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxRunOutputLines));

            RaisePropertyChanged(nameof(RunOutputText));
        });
    }

    private void ClearRunOutput()
    {
        RunOutputText = string.Empty;
        RaisePropertyChanged(nameof(RunOutputText));
    }

    private void BeginRunOutputSession(bool clearPrevious, string? banner = null)
    {
        RunOnUiThread(() =>
        {
            if (clearPrevious)
                RunOutputText = string.Empty;

            if (!string.IsNullOrWhiteSpace(banner))
                AppendRunOutput(banner);
        });
    }

    private static string ShortenForRunOutput(string? text, int maxLen = 480)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var first = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0);

        if (string.IsNullOrWhiteSpace(first))
            first = text.Trim();

        return first.Length <= maxLen ? first : first[..maxLen] + "…";
    }

    private static string ShortenException(Exception ex) =>
        ShortenForRunOutput(ex.InnerException?.Message ?? ex.Message);

    private string FormatRunLogEntry(WorkflowRunLogEntry entry)
    {
        var stepLabel = entry.StepId == Guid.Empty
            ? "流程"
            : _steps.FirstOrDefault(s => s.Id == entry.StepId)?.Title ?? entry.Kind;

        if (entry.Success)
            return $"[{stepLabel}] {entry.Message}";

        var detail = ShortenForRunOutput(entry.Error);
        if (string.Equals(entry.Message, "Step failed.", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(detail)
                ? $"[{stepLabel}] 步骤失败"
                : $"[{stepLabel}] {detail}";
        }

        var summary = ShortenForRunOutput(entry.Message);
        return string.IsNullOrWhiteSpace(detail) || detail == summary
            ? $"[{stepLabel}] {summary}"
            : $"[{stepLabel}] {summary} — {detail}";
    }

    private static bool ShouldShowInRunOutput(WorkflowRunLogEntry entry)
    {
        if (!entry.Success)
            return true;

        if (entry.Message is "Step started." or "Step completed.")
            return false;

        if (entry.Message.StartsWith("Reading ", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private async void ExecuteSaveScript()
    {
        try
        {
            if (!IsScriptStepSelected || SelectedStep is null)
                return;

            var normalized = ScriptDraft ?? string.Empty;
            var refsNormalized = ScriptReferencesDraft ?? string.Empty;

            var scriptUnchanged = SelectedStep.Parameters.GetValueOrDefault("script", string.Empty) == normalized;
            var refsUnchanged = SelectedStep.Parameters.GetValueOrDefault("references", string.Empty) == refsNormalized;
            if (scriptUnchanged && refsUnchanged)
            {
                AppendEditorLog("脚本/引用未变化，无需保存。");
                return;
            }

            RecordUndoCheckpointForField(SelectedStep.Id, "param:script");
            SelectedStep.Parameters["script"] = normalized;
            SelectedStep.Parameters["references"] = refsNormalized;
            MarkDirty();

            // 保存脚本时一并落盘，避免仅关面板导致脚本草稿丢失。
            AppendEditorLog("脚本/引用已写入步骤参数，正在保存到磁盘…");
            await SaveAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendEditorLog($"保存脚本失败：{ex}", "ERROR");
            SaveStatusText = $"保存失败：{ex.Message}";
            IsDirty = true;
        }
    }

    private void CompileScriptDraft()
    {
        if (!IsScriptStepSelected)
            return;

        var code = ScriptDraft ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            AppendEditorLog("脚本为空，无法编译。", "WARN");
            return;
        }

        var refs = (ScriptReferencesDraft ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
        var result = _scriptCompiler.Compile(code, refs);
        if (result.Success)
        {
            AppendEditorLog("脚本编译通过。");
            return;
        }

        AppendEditorLog("脚本编译失败：", "ERROR");
        foreach (var err in result.Errors)
            AppendEditorLog(err, "ERROR");
    }

    private async Task BrowseDllReferencesAsync()
    {
        if (!IsScriptStepSelected)
            return;

        var hwnd = WorkflowAppServices.ResolveMainWindowHandle();
        if (hwnd == IntPtr.Zero)
        {
            AppendEditorLog("无法获取主窗口句柄，暂时不能打开文件选择器。", "ERROR");
            return;
        }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = PickerViewMode.List;
        picker.FileTypeFilter.Add(".dll");
        picker.FileTypeFilter.Add(".exe");

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
            return;

        var existing = new HashSet<string>(
            (ScriptReferencesDraft ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var f in files)
        {
            if (!string.IsNullOrWhiteSpace(f.Path))
                existing.Add(f.Path);
        }

        ScriptReferencesDraft = string.Join(Environment.NewLine, existing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        RaisePropertyChanged(nameof(ScriptReferencesDraft));
        AppendEditorLog($"已添加引用：{files.Count} 个文件。");
        BrowseDllReferencesCommand.RaiseCanExecuteChanged();
    }

    private void LoadScriptDraftFromSelected()
    {
        if (!IsScriptStepSelected || SelectedStep is null)
        {
            ScriptDraft = string.Empty;
            ScriptReferencesDraft = string.Empty;
            RaisePropertyChanged(nameof(ScriptDraft));
            RaisePropertyChanged(nameof(ScriptReferencesDraft));
            return;
        }

        ScriptDraft = SelectedStep.Parameters.GetValueOrDefault("script", string.Empty);
        ScriptReferencesDraft = SelectedStep.Parameters.GetValueOrDefault("references", string.Empty);
        RaisePropertyChanged(nameof(ScriptDraft));
        RaisePropertyChanged(nameof(ScriptReferencesDraft));
    }

    private IWorkflowRuntime Runtime
    {
        get
        {
            if (_runtime is not null)
                return _runtime;

            _runtime = WorkflowAppServices.ResolveRuntime();
            _runtime.Changed += OnRuntimeChanged;
            return _runtime;
        }
    }

    /// <summary>画布拖拽期间抑制脏标记与自动保存，避免卡顿。</summary>
    public void BeginCanvasInteraction()
    {
        if (_canvasInteractionDepth == 0)
            RecordUndoCheckpoint();
        _canvasInteractionDepth++;
    }

    public void EndCanvasInteraction()
    {
        if (_canvasInteractionDepth > 0)
            _canvasInteractionDepth--;
    }

    public async Task TryRecoverAutosaveDeferredAsync()
    {
        await Task.Yield();
        NavTrace.Write("WorkflowEditor.TryRecoverAutosaveDeferred: start");
        await TryRecoverAutosaveIfNeededAsync().ConfigureAwait(true);
        NavTrace.Write("WorkflowEditor.TryRecoverAutosaveDeferred: done");
    }

    public async Task LoadFromNavigationAsync(INavigationParameters? parameters)
    {
        NavTrace.Write("WorkflowEditor.LoadFromNavigationAsync: start");
        parameters ??= WorkflowNavigationBridge.TakePending();
        _skipRecoveryPrompt = parameters is not null && HasNavigationFlag(parameters, "isNew");
        var idStr = parameters?["workflowId"] as string;
        if (!Guid.TryParseExact(idStr, "N", out var id))
        {
            if (_steps.Count == 0)
                ApplyWorkflow(CreateDefaultWorkflowDefinition());
            return;
        }

        if (id == _workflow.Id && _steps.Count > 0)
            return;

        var wf = await _store.LoadAsync(id).ConfigureAwait(true);
        if (wf is null)
        {
            SaveStatusText = "流程不存在或无法读取";
            if (_steps.Count == 0)
                ApplyWorkflow(CreateDefaultWorkflowDefinition());
            return;
        }

        ApplyWorkflow(wf);
        NavTrace.Write($"WorkflowEditor.LoadFromNavigationAsync: loaded steps={_steps.Count}");
    }

    private static WorkflowDefinition CreateDefaultWorkflowDefinition()
    {
        var wf = new WorkflowDefinition
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
                        ["symbol"] = "GVL.bRun",
                        ["type"] = "BOOL",
                        ["value"] = "true",
                    }
                },
            }
        };

        wf.Steps[0].NextStepId = wf.Steps[1].Id;
        wf.Steps[1].NextStepId = wf.Steps[2].Id;
        return wf;
    }

    private void ApplyWorkflow(WorkflowDefinition wf)
    {
        _workflow = wf;
        _steps.Clear();
        foreach (var s in _workflow.Steps)
            _steps.Add(s);

        Links.Clear();
        if (_workflow.Links.Count > 0)
        {
            foreach (var l in DedupeLinksByStepPair(_workflow.Links))
                Links.Add(l);
        }
        else
        {
            // 兼容旧模型：用 NextStepId/Branches 推导显示用连线
            foreach (var s in _steps)
            {
                if (s.NextStepId is { } next)
                    TryAddLink(s.Id, WorkflowPort.Right, next, WorkflowPort.Left);

                foreach (var b in s.Branches)
                    TryAddLink(s.Id, WorkflowPort.Right, b.NextStepId, WorkflowPort.Left, b.Label);
            }
        }

        SelectedStep = _steps.FirstOrDefault();
        TrackStepPropertyChanges();
        IsDirty = false;
        SaveStatusText = "已保存";
        _explicitSavePerformed = false;
        RaisePropertyChanged(nameof(SubTitle));
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(WorkflowName));
        RaisePropertyChanged(nameof(CanvasDebugText));
        _history.Clear();
        RefreshUndoCommands();
        AppendEditorLog($"已加载流程「{_workflow.Name}」：步骤 {_steps.Count} 个，连线 {Links.Count} 条。");
        RefreshAvailableDevicesBestEffort();
    }

    private void InitializeToolbox()
    {
        ToolboxItems.Clear();
        foreach (var module in _moduleCatalog.GetAll())
            ToolboxItems.Add(new WorkflowToolboxItem(module));
    }

    private void RefreshStepPropertyFields()
    {
        StepPropertyFields.Clear();
        if (SelectedStep is null)
        {
            RaisePropertyChanged(nameof(HasStepPropertyFields));
            RaisePropertyChanged(nameof(ShowNoExtraParamsHint));
            return;
        }

        TryApplyDefaultDevice(SelectedStep);

        var module = _moduleCatalog.GetModule(SelectedStep.Kind);
        if (module is not null)
        {
            foreach (var definition in module.Properties)
            {
                if (IsScriptStepSelected &&
                    (string.Equals(definition.Key, "script", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(definition.Key, "references", StringComparison.OrdinalIgnoreCase)))
                    continue;

                StepPropertyFields.Add(new WorkflowStepPropertyFieldViewModel(
                    SelectedStep,
                    definition,
                    deviceOptions: AvailableDevices.ToList(),
                    typeOptions: PlcTypeOptions,
                    onBeforeChanged: () => RecordUndoCheckpointForField(SelectedStep.Id, $"param:{definition.Key}"),
                    onChanged: MarkDirty));
            }
        }

        RaisePropertyChanged(nameof(HasStepPropertyFields));
        RaisePropertyChanged(nameof(IsScriptStepSelected));
        LoadScriptDraftFromSelected();
        RaisePropertyChanged(nameof(ShowNoExtraParamsHint));
        SaveScriptCommand.RaiseCanExecuteChanged();
        CompileScriptCommand.RaiseCanExecuteChanged();
        BrowseDllReferencesCommand.RaiseCanExecuteChanged();
    }

    private void AddStep() =>
        AddStepFromToolbox(ToolboxItems.FirstOrDefault(i => i.Kind == "Plc.Ads.Read"));

    public void AddStepFromToolbox(WorkflowToolboxItem? item) =>
        AddStepFromToolboxAt(item, null, null);

    public void AddStepFromToolboxAt(WorkflowToolboxItem? item, double? x, double? y)
    {
        if (item is null)
            return;

        RecordUndoCheckpoint();
        var step = CreateStepFromToolbox(item, x, y);
        _steps.Add(step);
        SelectedStep = step;
        TrackStepPropertyChanges();
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(CanvasDebugText));
        AppendEditorLog($"已添加步骤：{step.Title} ({step.Kind})");
        MarkDirty();
    }

    public void CopySelectedStep()
    {
        if (SelectedStep is null && SelectedSteps.Count == 0)
            return;

        _clipboardSteps.Clear();

        if (SelectedSteps.Count > 0)
        {
            foreach (var step in SelectedSteps)
                _clipboardSteps.Add(CloneStepSnapshot(step));
        }
        else if (SelectedStep is not null)
        {
            _clipboardSteps.Add(CloneStepSnapshot(SelectedStep));
        }

        _pasteGeneration = 0;
        var count = _clipboardSteps.Count;
        var name = count == 1 ? _clipboardSteps[0].Title : $"{count} 个步骤";
        AppendEditorLog($"已复制：{name}");
    }

    public bool PasteStep(double? x = null, double? y = null)
    {
        if (_clipboardSteps.Count == 0)
            return false;

        RecordUndoCheckpoint();
        _pasteGeneration++;
        const double pasteOffset = 32;
        var delta = pasteOffset * _pasteGeneration;

        // Reference point：第一个复制的步骤
        var origin = _clipboardSteps[0];
        var pastedSteps = new List<WorkflowStep>();

        foreach (var src in _clipboardSteps)
        {
            var dx = src.X - origin.X;
            var dy = src.Y - origin.Y;
            var pasteX = (x ?? origin.X) + dx + delta;
            var pasteY = (y ?? origin.Y) + dy + delta;

            var pasted = new WorkflowStep
            {
                Title = src.Title,
                Kind = src.Kind,
                X = pasteX,
                Y = pasteY,
                Notes = src.Notes,
                Parameters = new Dictionary<string, string>(src.Parameters, StringComparer.OrdinalIgnoreCase),
            };

            pastedSteps.Add(pasted);
            _steps.Add(pasted);
        }

        SetSelection(pastedSteps);
        TrackStepPropertyChanges();
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(CanvasDebugText));
        AppendEditorLog($"已粘贴：{pastedSteps.Count} 个步骤");
        MarkDirty();
        return true;
    }

    /// <summary>删除当前选中的步骤（支持多选）。</summary>
    public bool DeleteSelectedSteps()
    {
        List<WorkflowStep> toDelete;
        if (_selectedSteps.Count > 0)
            toDelete = _selectedSteps.ToList();
        else if (_selectedStep is not null)
            toDelete = [_selectedStep];
        else
            return false;

        RecordUndoCheckpoint();
        var deleteIds = toDelete.Select(s => s.Id).ToHashSet();

        if (_linkFrom is not null && deleteIds.Contains(_linkFrom.Id))
            _linkFrom = null;

        foreach (var step in toDelete)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
            _trackedSteps.Remove(step);
            _steps.Remove(step);
        }

        for (var i = Links.Count - 1; i >= 0; i--)
        {
            var link = Links[i];
            if (deleteIds.Contains(link.FromStepId) || deleteIds.Contains(link.ToStepId))
                Links.RemoveAt(i);
        }

        foreach (var survivor in _steps)
        {
            if (survivor.NextStepId is { } next && deleteIds.Contains(next))
                survivor.NextStepId = null;

            survivor.Branches.RemoveAll(b => deleteIds.Contains(b.NextStepId));
        }

        ClearSelection();
        TrackStepPropertyChanges();
        RaisePropertyChanged(nameof(Steps));
        RaisePropertyChanged(nameof(CanvasDebugText));

        var count = toDelete.Count;
        var label = count == 1 ? toDelete[0].Title : $"{count} 个步骤";
        AppendEditorLog($"已删除：{label}");
        MarkDirty();
        return true;
    }

    private static WorkflowStep CloneStepSnapshot(WorkflowStep source) =>
        WorkflowEditorSnapshot.CloneStep(source);

    private WorkflowStep CreateStepFromToolbox(WorkflowToolboxItem item, double? x, double? y)
    {
        var offset = _steps.Count;
        var step = new WorkflowStep
        {
            Title = item.Title,
            Kind = item.Kind,
            X = x ?? 40 + (offset % 4) * 48,
            Y = y ?? 120 + offset * 72,
        };

        _moduleCatalog.GetModule(item.Kind)?.ApplyDefaults(step);
        TryApplyDefaultDevice(step);
        return step;
    }

    public void BeginLinkFromPort(WorkflowStep step, WorkflowPort port)
    {
        _linkFrom = step;
        _linkFromPort = port;
    }

    public void CancelLinkInProgress()
    {
        _linkFrom = null;
    }

    public void TryCompleteLinkToPort(WorkflowStep toStep, WorkflowPort toPort)
    {
        if (_linkFrom is null)
            return;
        if (ReferenceEquals(_linkFrom, toStep))
            return;

        var fromTitle = _linkFrom.Title;
        var beforeLink = CreateSnapshot();
        if (!TryAddLink(_linkFrom.Id, _linkFromPort, toStep.Id, toPort))
        {
            SaveStatusText = "同一模块之间仅允许一条连线";
            AppendEditorLog($"连线被拒绝：{fromTitle} → {toStep.Title}（同一模块对仅允许一条连线）", "WARN");
            _linkFrom = null;
            return;
        }

        RecordUndoCheckpoint(beforeLink);
        AppendEditorLog($"已连线：{fromTitle} ({_linkFromPort}) → {toStep.Title} ({toPort})");
        if (_linkFromPort == WorkflowPort.Right && toPort == WorkflowPort.Left)
            _linkFrom.NextStepId = toStep.Id;
        _linkFrom = null;
        MarkDirty();
    }

    private static IEnumerable<WorkflowLink> DedupeLinksByStepPair(IEnumerable<WorkflowLink> links)
    {
        var seen = new HashSet<(Guid From, Guid To)>();
        foreach (var link in links)
        {
            if (seen.Add((link.FromStepId, link.ToStepId)))
                yield return link;
        }
    }

    private bool TryAddLink(Guid fromStepId, WorkflowPort fromPort, Guid toStepId, WorkflowPort toPort, string? label = null)
    {
        if (fromStepId == toStepId)
            return false;

        if (Links.Any(l => l.FromStepId == fromStepId && l.ToStepId == toStepId))
            return false;

        if (Links.Any(l => l.FromStepId == fromStepId && l.FromPort == fromPort && l.ToStepId == toStepId && l.ToPort == toPort))
            return false;

        Links.Add(new WorkflowLink
        {
            FromStepId = fromStepId,
            FromPort = fromPort,
            ToStepId = toStepId,
            ToPort = toPort,
            Label = label,
        });
        return true;
    }

    private async Task SaveAsync()
    {
        if (IsSaving)
        {
            AppendEditorLog("正在保存中，请稍候…", "WARN");
            return;
        }

        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expectedPath = Path.Combine(baseDir, "KJ", "workflows", $"{_workflow.Id:N}.json");
        DateTime? beforeWriteUtc = null;
        try
        {
            if (File.Exists(expectedPath))
                beforeWriteUtc = File.GetLastWriteTimeUtc(expectedPath);
        }
        catch
        {
            // ignore IO issues for debug stamp
        }

        AppendEditorLog($"保存开始：wf={_workflow.Id:N} → {expectedPath}（beforeUtc={(beforeWriteUtc is null ? "null" : beforeWriteUtc.Value.ToString("O"))}）");

        // 顶栏“保存”应当视为最终提交：如果脚本仍停留在草稿区，也一并写回步骤参数再落盘。
        if (IsScriptStepSelected && SelectedStep is not null)
        {
            var draft = ScriptDraft ?? string.Empty;
            var current = SelectedStep.Parameters.GetValueOrDefault("script", string.Empty);
            if (!string.Equals(draft, current, StringComparison.Ordinal))
                SelectedStep.Parameters["script"] = draft;

            var refsDraft = ScriptReferencesDraft ?? string.Empty;
            var refsCurrent = SelectedStep.Parameters.GetValueOrDefault("references", string.Empty);
            if (!string.Equals(refsDraft, refsCurrent, StringComparison.Ordinal))
                SelectedStep.Parameters["references"] = refsDraft;
        }

        _workflow.Steps = _steps.ToList();
        if (_workflow.Links.Count > 0 || Links.Count > 0)
            _workflow.Links = Links.ToList();
        try
        {
            IsSaving = true;
            SaveStatusText = "正在保存…";
            AppendEditorLog("正在保存流程…");
            await _store.SaveAsync(_workflow).ConfigureAwait(true);
            await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
            IsDirty = false;
            SaveStatusText = "已保存";
            _explicitSavePerformed = true;
            RaisePropertyChanged(nameof(SubTitle));
            DateTime? afterWriteUtc = null;
            try
            {
                if (File.Exists(expectedPath))
                    afterWriteUtc = File.GetLastWriteTimeUtc(expectedPath);
            }
            catch
            {
                // ignore
            }

            AppendEditorLog($"保存成功：{_workflow.Name}（步骤 {_steps.Count}，连线 {Links.Count}，afterUtc={(afterWriteUtc is null ? "null" : afterWriteUtc.Value.ToString("O"))}）");

            // 立即从磁盘回读一次验证，避免“看起来没保存”的误判
            var verify = await _store.LoadAsync(_workflow.Id).ConfigureAwait(true);
            if (verify is null)
            {
                AppendEditorLog("保存后校验失败：磁盘回读为 null。", "ERROR");
            }
            else
            {
                AppendEditorLog($"保存后校验：disk.updatedAt={verify.UpdatedAt:O} steps={verify.Steps.Count} links={verify.Links.Count}");
            }
        }
        catch (Exception ex)
        {
            SaveStatusText = $"保存失败：{ex.Message}";
            AppendEditorLog($"保存失败：{ex}", "ERROR");
            IsDirty = true;
        }
        finally
        {
            IsSaving = false;
            RefreshRunCommands();
        }
    }

    private bool CanStartContinuous()
    {
        if (IsSaving)
            return false;

        if (_runtime is null)
            return true;

        return _runtime.State is WorkflowRunState.Idle or WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Canceled;
    }

    private bool CanStep()
    {
        if (IsSaving)
            return false;

        if (_runtime is null)
            return true;

        return _runtime.State is WorkflowRunState.Paused
            or WorkflowRunState.Idle
            or WorkflowRunState.Completed
            or WorkflowRunState.Failed
            or WorkflowRunState.Canceled;
    }

    private WorkflowDefinition CreateSnapshot() =>
        WorkflowEditorSnapshot.Clone(new WorkflowDefinition
        {
            Id = _workflow.Id,
            Name = _workflow.Name,
            Version = _workflow.Version,
            UpdatedAt = _workflow.UpdatedAt,
            Links = Links.ToList(),
            Steps = _steps.ToList(),
        });

    public void RecordUndoCheckpoint(WorkflowDefinition? snapshot = null)
    {
        if (_isRestoringHistory)
            return;

        _undoCoalesceToken = null;
        _history.Push(snapshot ?? CreateSnapshot());
        RefreshUndoCommands();
    }

    private void RecordUndoCheckpointForField(Guid stepId, string field)
    {
        if (_isRestoringHistory)
            return;

        var token = (stepId, field);
        if (_undoCoalesceToken == token)
            return;

        _undoCoalesceToken = token;
        _history.Push(CreateSnapshot());
        RefreshUndoCommands();
    }

    public void Undo()
    {
        if (!_history.CanUndo)
            return;

        var previous = _history.PopUndo(CreateSnapshot());
        if (previous is null)
            return;

        ApplyEditorSnapshot(previous);
        AppendEditorLog("已撤回上一步操作。");
    }

    public void Redo()
    {
        if (!_history.CanRedo)
            return;

        var next = _history.PopRedo(CreateSnapshot());
        if (next is null)
            return;

        ApplyEditorSnapshot(next);
        AppendEditorLog("已重做操作。");
    }

    private void ApplyEditorSnapshot(WorkflowDefinition snapshot)
    {
        _isRestoringHistory = true;
        try
        {
            var selectedIds = _selectedSteps.Select(s => s.Id).ToHashSet();
            var primaryId = _selectedStep?.Id;

            _workflow.Name = snapshot.Name;
            RaisePropertyChanged(nameof(WorkflowName));

            _steps.Clear();
            foreach (var s in snapshot.Steps)
                _steps.Add(WorkflowEditorSnapshot.CloneStep(s));

            Links.Clear();
            foreach (var l in snapshot.Links)
            {
                Links.Add(new WorkflowLink
                {
                    FromStepId = l.FromStepId,
                    FromPort = l.FromPort,
                    ToStepId = l.ToStepId,
                    ToPort = l.ToPort,
                    Label = l.Label,
                });
            }

            var restored = _steps.Where(s => selectedIds.Contains(s.Id)).ToList();
            if (restored.Count > 0)
                SetSelection(restored);
            else if (primaryId is { } id)
                SelectedStep = _steps.FirstOrDefault(s => s.Id == id) ?? _steps.FirstOrDefault();
            else
                SelectedStep = _steps.FirstOrDefault();

            TrackStepPropertyChanges();
            RefreshStepPropertyFields();
            RaisePropertyChanged(nameof(Steps));
            RaisePropertyChanged(nameof(CanvasDebugText));
            MarkDirty();
        }
        finally
        {
            _isRestoringHistory = false;
            RefreshUndoCommands();
        }
    }

    private void RefreshUndoCommands()
    {
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private async Task RunContinuousAsync()
    {
        try
        {
            if (!ValidatePlcAdsStepsBeforeRun(out var validationError))
            {
                SaveStatusText = validationError;
                AppendRunOutput(validationError!, "WARN");
                return;
            }

            IsSaving = true;
            RefreshRunCommands();
            SaveStatusText = "运行中…";
            BeginRunOutputSession(clearPrevious: true, "—— 开始连续运行 ——");
            await Runtime.StartContinuousAsync(CreateSnapshot()).ConfigureAwait(true);
            FlushRunOutputFromStore();
        }
        catch (Exception ex)
        {
            SaveStatusText = $"运行失败：{ex.Message}";
            AppendRunOutput($"运行失败：{ShortenException(ex)}", "ERROR");
            FlushRunOutputFromStore();
        }
        finally
        {
            IsSaving = false;
            RefreshRunCommands();
        }
    }

    private async Task StepAsync()
    {
        try
        {
            if (!ValidatePlcAdsStepsBeforeRun(out var validationError))
            {
                SaveStatusText = validationError;
                AppendRunOutput(validationError!, "WARN");
                return;
            }

            IsSaving = true;
            RefreshRunCommands();

            if (Runtime.State is WorkflowRunState.Idle or WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Canceled)
            {
                SaveStatusText = "单步中…";
                BeginRunOutputSession(clearPrevious: true, "—— 开始单步运行 ——");
                await Runtime.StartStepAsync(CreateSnapshot()).ConfigureAwait(true);
            }
            else
            {
                SaveStatusText = "单步中…";
                AppendRunOutput("执行下一步…");
                await Runtime.StepOnceAsync().ConfigureAwait(true);
            }

            FlushRunOutputFromStore();
        }
        catch (Exception ex)
        {
            SaveStatusText = $"单步失败：{ex.Message}";
            AppendRunOutput($"单步失败：{ShortenException(ex)}", "ERROR");
            FlushRunOutputFromStore();
        }
        finally
        {
            IsSaving = false;
            RefreshRunCommands();
        }
    }

    private void Pause()
    {
        Runtime.Pause();
        SaveStatusText = "已暂停";
        AppendEditorLog("流程运行已暂停。");
        RefreshRunCommands();
    }

    private void Resume()
    {
        Runtime.Resume();
        SaveStatusText = "运行中…";
        AppendEditorLog("流程运行已恢复。");
        RefreshRunCommands();
    }

    private void Cancel()
    {
        Runtime.Cancel();
        SaveStatusText = "已取消";
        AppendEditorLog("流程运行已取消。");
        RefreshRunCommands();
    }

    private Guid? _lastDumpedRunId;
    private int _lastDumpedRunLogCount;

    private void FlushRunOutputFromStore() =>
        RunOnUiThread(DumpRecentRunLogsBestEffort);

    private void DumpRecentRunLogsBestEffort()
    {
        try
        {
            var runId = _runtime?.ActiveRunId;
            if (runId is null)
                return;

            if (_lastDumpedRunId != runId)
            {
                _lastDumpedRunId = runId;
                _lastDumpedRunLogCount = 0;
            }

            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetService(typeof(IWorkflowRunLogStore)) as IWorkflowRunLogStore;
            if (store is null)
                return;

            var entries = store.GetRecent(200)
                .Where(e => e.RunId == runId.Value)
                .OrderBy(e => e.Timestamp)
                .ToList();

            if (entries.Count <= _lastDumpedRunLogCount)
                return;

            foreach (var e in entries.Skip(_lastDumpedRunLogCount))
            {
                if (!ShouldShowInRunOutput(e))
                    continue;

                var line = FormatRunLogEntry(e);
                AppendRunOutput(line, e.Success ? "INFO" : "ERROR");
            }

            _lastDumpedRunLogCount = entries.Count;
        }
        catch
        {
            // ignore
        }
    }

    private void OnRuntimeChanged()
    {
        RunOnUiThread(() =>
        {
            RaisePropertyChanged(nameof(RuntimeCurrentStepId));
            RaisePropertyChanged(nameof(RuntimeHint));
            RefreshRunCommands();
            DumpRecentRunLogsBestEffort();
        });
    }

    private void RefreshRunCommands()
    {
        RunCommand.RaiseCanExecuteChanged();
        StepCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    public async Task BeginEditorSessionAsync()
    {
        // 如果上次异常退出留下 autosave，会导致“未点保存但重开仍是修改后内容”的错觉/体验问题。
        // 当前版本已禁用自动保存调度，因此进入编辑器时直接清理遗留的 autosave。
        await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
        await _store.MarkEditorSessionOpenAsync(_workflow.Id).ConfigureAwait(true);
    }

    public async Task EndEditorSessionAsync()
    {
        // 用户明确要求：不点保存就“不做修改”。因此离开编辑器时如未显式保存，则丢弃本次会话的内存更改。
        if (IsDirty && !_explicitSavePerformed)
        {
            var official = await _store.LoadAsync(_workflow.Id).ConfigureAwait(true);
            if (official is not null)
            {
                ApplyWorkflow(official);
                AppendEditorLog("已丢弃未保存的更改（已恢复为磁盘版本）。", "WARN");
            }
        }

        await _store.DeleteAutosaveAsync(_workflow.Id).ConfigureAwait(true);
        await _store.MarkEditorSessionClosedAsync().ConfigureAwait(true);
    }

    /// <summary>关闭属性面板：仅撤销脚本编辑区未点「保存脚本」的草稿，不改动画布上的步骤。</summary>
    public void OnPropertiesPaneClosed()
    {
        if (!IsScriptStepSelected)
            return;

        var step = SelectedStep;
        if (step is null)
            return;

        var savedScript = step.Parameters.GetValueOrDefault("script", string.Empty);
        var savedRefs = step.Parameters.GetValueOrDefault("references", string.Empty);
        var draftScript = ScriptDraft ?? string.Empty;
        var draftRefs = ScriptReferencesDraft ?? string.Empty;

        if (string.Equals(draftScript, savedScript, StringComparison.Ordinal) &&
            string.Equals(draftRefs, savedRefs, StringComparison.Ordinal))
            return;

        LoadScriptDraftFromSelected();
        AppendEditorLog("已撤销属性面板中未保存的脚本草稿。", "WARN");
    }

    public void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        // WinUI 区域导航为同步等待；任何弹窗/异步确认都会导致卡死。直接放行。
        NavTrace.Write("WorkflowEditor.ConfirmNavigation: allow");
        continuationCallback(true);
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        NavTrace.Write("WorkflowEditor.OnNavigatedTo (region callback)");
        _skipRecoveryPrompt = HasNavigationFlag(navigationContext.Parameters, "isNew");
        WorkflowNavigationBridge.SetPending(navigationContext.Parameters);
    }

    private static bool HasNavigationFlag(INavigationParameters parameters, string key)
    {
        if (!parameters.ContainsKey(key))
            return false;

        return parameters[key] switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            _ => true,
        };
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

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
        if (_canvasInteractionDepth > 0)
            return;

        IsDirty = true;
        if (!IsSaving)
            SaveStatusText = "● 未保存";
    }

    public void MarkDirtyAfterCanvasInteraction()
    {
        if (_canvasInteractionDepth > 0)
            return;

        MarkDirty();
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
            await Task.Delay(2000, ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();

            var snapshot = CreateSnapshot();

            IsSaving = true;
            SaveStatusText = "正在保存…";
            await _store.SaveAutosaveAsync(snapshot, ct).ConfigureAwait(true);

            if (!ct.IsCancellationRequested)
            {
                // 只有在保存期间没有新的修改时才清除 IsDirty
                // 如果用户在保存期间又改了，MarkDirty 会重新设为 true
                if (!_isDirty)
                    SaveStatusText = "已保存";
                else
                    SaveStatusText = "● 未保存";
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
            s.PropertyChanged += OnStepPropertyChanged;
        }

        // 清理已移除步骤的事件订阅
        var toRemove = _trackedSteps.Where(s => !_steps.Contains(s)).ToList();
        foreach (var s in toRemove)
        {
            s.PropertyChanged -= OnStepPropertyChanged;
            _trackedSteps.Remove(s);
        }
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WorkflowStep.X) or nameof(WorkflowStep.Y))
        {
            if (_canvasInteractionDepth == 0)
                MarkDirty();
            return;
        }

        if (args.PropertyName is nameof(WorkflowStep.NextStepId))
            MarkDirty();
    }

    private async Task TryRecoverAutosaveIfNeededAsync()
    {
        if (_skipRecoveryPrompt)
            return;

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

