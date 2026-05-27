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

    /// <summary>
    /// 图形连线（用于编辑器展示/多连线/多端口）。运行时仍可回退使用 Step.NextStepId / Step.Branches。
    /// </summary>
    public List<WorkflowLink> Links { get; set; } = [];
}

public enum WorkflowPort
{
    Top = 0,
    Right = 1,
    Bottom = 2,
    Left = 3,
}

public sealed class WorkflowLink
{
    public Guid FromStepId { get; set; }
    public WorkflowPort FromPort { get; set; } = WorkflowPort.Right;
    public Guid ToStepId { get; set; }
    public WorkflowPort ToPort { get; set; } = WorkflowPort.Left;

    /// <summary>可选标签（如 Decision 分支名）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }
}

/// <summary>条件分支：当条件满足时跳转到目标步骤。</summary>
public sealed class WorkflowBranch
{
    /// <summary>分支标签（用于 UI 显示）。</summary>
    public string Label { get; set; } = "";

    /// <summary>目标步骤 ID。</summary>
    public Guid NextStepId { get; set; }

    /// <summary>条件表达式（如 "temperature > 100"、"status == 'running'"）。</summary>
    public string Condition { get; set; } = "";

    /// <summary>条件类型。</summary>
    public BranchConditionType ConditionType { get; set; } = BranchConditionType.Expression;
}

public enum BranchConditionType
{
    /// <summary>表达式求值（如 "tag:temp > 100"）。</summary>
    Expression = 0,

    /// <summary>标签值匹配（Parameters[key] == expectedValue）。</summary>
    TagEquals = 1,

    /// <summary>标签值大于阈值。</summary>
    TagGreaterThan = 2,

    /// <summary>标签值小于阈值。</summary>
    TagLessThan = 3,

    /// <summary>默认/兜底分支（无条件匹配）。</summary>
    Default = 99,
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

    /// <summary>画布位置(像素坐标)。</summary>
    private double _x;
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>画布位置(像素坐标)。</summary>
    private double _y;
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>默认仅支持"下一步"连线（最小可用）。</summary>
    private Guid? _nextStepId;
    public Guid? NextStepId
    {
        get => _nextStepId;
        set => SetProperty(ref _nextStepId, value);
    }

    /// <summary>条件分支列表。当 Kind="Decision" 时，按顺序评估条件，首个匹配的分支生效。</summary>
    public List<WorkflowBranch> Branches { get; set; } = new();

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

