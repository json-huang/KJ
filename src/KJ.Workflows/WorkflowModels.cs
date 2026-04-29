using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace KJ.Workflows;

public sealed class WorkflowDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新流程";
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public List<WorkflowStep> Steps { get; set; } = [];
}

public sealed class WorkflowStep : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _title = "新步骤";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _kind = "Action";
    public string Kind
    {
        get => _kind;
        set => SetProperty(ref _kind, value);
    }

    /// <summary>画布位置（像素坐标）。</summary>
    private double _x;
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>画布位置（像素坐标）。</summary>
    private double _y;
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>默认仅支持“下一步”连线（最小可用）。</summary>
    private Guid? _nextStepId;
    public Guid? NextStepId
    {
        get => _nextStepId;
        set => SetProperty(ref _nextStepId, value);
    }

    /// <summary>任意参数（用于后续扩展：PLC/自定义TCP/脚本等）。</summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T backing, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backing, value))
            return;
        backing = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

