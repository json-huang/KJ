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
        Action? onBeforeChanged = null,
        Action? onChanged = null)
    {
        _step = step;
        _definition = definition;
        _onBeforeChanged = onBeforeChanged;
        _onChanged = onChanged;
    }

    public string Key => _definition.Key;
    public string Label => _definition.Label;
    public string? Placeholder => _definition.Placeholder;
    public bool IsReadOnly => _definition.IsReadOnly;

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
            _onChanged?.Invoke();
        }
    }
}
