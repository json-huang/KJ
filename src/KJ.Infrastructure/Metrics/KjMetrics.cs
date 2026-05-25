using System.Diagnostics.Metrics;

namespace KJ.Infrastructure.Metrics;

/// <summary>
/// KJ 系统指标。使用 .NET Meter API（OpenTelemetry 兼容）。
/// 
/// 支持的指标：
/// - 设备连接状态
/// - 标签采集延迟
/// - 标签采集成功/失败计数
/// - 告警触发计数
/// - 工作流执行计数
/// - 数据库操作延迟
/// </summary>
public sealed class KjMetrics : IDisposable
{
    public const string MeterName = "KJ";

    private readonly Meter _meter;

    // 设备指标
    public Counter<long> DeviceConnectAttempts { get; }
    public Counter<long> DeviceConnectFailures { get; }
    public ObservableGauge<int> DevicesConnected { get; }
    public ObservableGauge<int> DevicesFaulted { get; }

    // 标签采集指标
    public Counter<long> TagReads { get; }
    public Counter<long> TagReadFailures { get; }
    public Histogram<double> TagReadDurationMs { get; }
    public Counter<long> TagWrites { get; }

    // 告警指标
    public Counter<long> AlarmsTriggered { get; }
    public Counter<long> AlarmsAcknowledged { get; }

    // 工作流指标
    public Counter<long> WorkflowRuns { get; }
    public Counter<long> WorkflowStepExecutions { get; }
    public Counter<long> WorkflowFailures { get; }

    // 数据库指标
    public Counter<long> DbOperations { get; }
    public Histogram<double> DbOperationDurationMs { get; }

    // 回调（用于 Observable Gauge）
    private Func<int>? _devicesConnectedFunc;
    private Func<int>? _devicesFaultedFunc;

    public KjMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        // 设备
        DeviceConnectAttempts = _meter.CreateCounter<long>("kj.device.connect.attempts", "attempts", "Device connection attempts");
        DeviceConnectFailures = _meter.CreateCounter<long>("kj.device.connect.failures", "failures", "Device connection failures");
        DevicesConnected = _meter.CreateObservableGauge("kj.device.connected", () => _devicesConnectedFunc?.Invoke() ?? 0, "devices", "Connected devices count");
        DevicesFaulted = _meter.CreateObservableGauge("kj.device.faulted", () => _devicesFaultedFunc?.Invoke() ?? 0, "devices", "Faulted devices count");

        // 标签采集
        TagReads = _meter.CreateCounter<long>("kj.tag.reads", "reads", "Tag read operations");
        TagReadFailures = _meter.CreateCounter<long>("kj.tag.read.failures", "failures", "Tag read failures");
        TagReadDurationMs = _meter.CreateHistogram<double>("kj.tag.read.duration", "ms", "Tag read duration");
        TagWrites = _meter.CreateCounter<long>("kj.tag.writes", "writes", "Tag write operations");

        // 告警
        AlarmsTriggered = _meter.CreateCounter<long>("kj.alarms.triggered", "alarms", "Alarms triggered");
        AlarmsAcknowledged = _meter.CreateCounter<long>("kj.alarms.acknowledged", "alarms", "Alarms acknowledged");

        // 工作流
        WorkflowRuns = _meter.CreateCounter<long>("kj.workflow.runs", "runs", "Workflow runs started");
        WorkflowStepExecutions = _meter.CreateCounter<long>("kj.workflow.steps", "steps", "Workflow step executions");
        WorkflowFailures = _meter.CreateCounter<long>("kj.workflow.failures", "failures", "Workflow failures");

        // 数据库
        DbOperations = _meter.CreateCounter<long>("kj.db.operations", "operations", "Database operations");
        DbOperationDurationMs = _meter.CreateHistogram<double>("kj.db.operation.duration", "ms", "Database operation duration");
    }

    /// <summary>注册设备状态回调。</summary>
    public void SetDeviceCountProviders(Func<int> connectedFunc, Func<int> faultedFunc)
    {
        _devicesConnectedFunc = connectedFunc;
        _devicesFaultedFunc = faultedFunc;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
