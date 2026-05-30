using KJ.Workflows;
using KJ.Workflows.Modules;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class WorkflowStepPropertyFieldViewModel : BindableBase
{
    private readonly WorkflowStep _step;
    private readonly WorkflowStepPropertyDefinition _definition;
    private readonly Action? _onBeforeChanged;
    private readonly Action? _onChanged;

    public WorkflowStepPropertyFieldViewModel(
        WorkflowStep step,
        WorkflowStepPropertyDefinition definition,
        IReadOnlyList<WorkflowEditorViewModel.DeviceOption>? deviceOptions = null,
        IReadOnlyList<string>? typeOptions = null,
        Action? onBeforeChanged = null,
        Action? onChanged = null)
    {
        _step = step;
        _definition = definition;
        DeviceOptions = deviceOptions ?? Array.Empty<WorkflowEditorViewModel.DeviceOption>();
        TypeOptions = typeOptions ?? Array.Empty<string>();
        _onBeforeChanged = onBeforeChanged;
        _onChanged = onChanged;
    }

    public string Key => _definition.Key;
    public string Label => _definition.Label;
    public string? Placeholder => _definition.Placeholder;
    public bool IsReadOnly => _definition.IsReadOnly;
    public bool IsMultiline => _definition.IsMultiline;
    public bool IsDeviceSelector => string.Equals(Key, "device", StringComparison.OrdinalIgnoreCase);
    public bool IsPlcTypeSelector => string.Equals(Key, "type", StringComparison.OrdinalIgnoreCase);
    public double EditorMinHeight => _definition.IsMultiline ? _definition.MinLines * 22 : 32;
    public double EditorMaxHeight => _definition.IsMultiline ? _definition.MinLines * 22 + 120 : 120;

    /// <summary>设备下拉选项（直接挂在字段 VM 上，避免 DataTemplate 里 ElementName 绑定失效）。</summary>
    public IReadOnlyList<WorkflowEditorViewModel.DeviceOption> DeviceOptions { get; }

    /// <summary>PLC 类型下拉选项。</summary>
    public IReadOnlyList<string> TypeOptions { get; }

    public string Value
    {
        get => _step.Parameters.GetValueOrDefault(Key, string.Empty);
        set
        {
            var normalized = value ?? string.Empty;
            if (_step.Parameters.GetValueOrDefault(Key, string.Empty) == normalized)
                return;

            _onBeforeChanged?.Invoke();
            _step.Parameters[Key] = normalized;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectedDevice));
            _onChanged?.Invoke();
        }
    }

    /// <summary>设备下拉当前项（用 SelectedItem 绑定，比 SelectedValue 更可靠）。</summary>
    public WorkflowEditorViewModel.DeviceOption? SelectedDevice
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value))
                return null;

            return DeviceOptions.FirstOrDefault(d =>
                string.Equals(d.DeviceId, Value, StringComparison.OrdinalIgnoreCase));
        }
        set
        {
            var deviceId = value?.DeviceId ?? string.Empty;
            if (string.Equals(Value, deviceId, StringComparison.Ordinal))
                return;

            Value = deviceId;
            RaisePropertyChanged(nameof(SelectedDevice));
        }
    }
}
