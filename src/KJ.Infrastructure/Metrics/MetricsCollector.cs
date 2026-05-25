using KJ.Domain;
using KJ.Domain.Services;

namespace KJ.Infrastructure.Metrics;

/// <summary>
/// 指标采集器。将 KjMetrics 连接到现有服务，自动收集指标。
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly KjMetrics _metrics;
    private readonly IAlarmService _alarmService;

    public MetricsCollector(KjMetrics metrics, IAlarmService alarmService)
    {
        _metrics = metrics;
        _alarmService = alarmService;

        _alarmService.AlarmRaised += (_, _) => _metrics.AlarmsTriggered.Add(1);
    }

    /// <summary>记录设备连接尝试。</summary>
    public void RecordDeviceConnect(bool success)
    {
        _metrics.DeviceConnectAttempts.Add(1);
        if (!success)
            _metrics.DeviceConnectFailures.Add(1);
    }

    /// <summary>记录标签读取。</summary>
    public void RecordTagRead(double durationMs, bool success)
    {
        _metrics.TagReads.Add(1);
        _metrics.TagReadDurationMs.Record(durationMs);
        if (!success)
            _metrics.TagReadFailures.Add(1);
    }

    /// <summary>记录标签写入。</summary>
    public void RecordTagWrite()
    {
        _metrics.TagWrites.Add(1);
    }

    /// <summary>记录工作流执行。</summary>
    public void RecordWorkflowRun() => _metrics.WorkflowRuns.Add(1);
    public void RecordWorkflowStep() => _metrics.WorkflowStepExecutions.Add(1);
    public void RecordWorkflowFailure() => _metrics.WorkflowFailures.Add(1);

    /// <summary>记录数据库操作。</summary>
    public void RecordDbOperation(double durationMs)
    {
        _metrics.DbOperations.Add(1);
        _metrics.DbOperationDurationMs.Record(durationMs);
    }

    /// <summary>记录告警确认。</summary>
    public void RecordAlarmAcknowledged() => _metrics.AlarmsAcknowledged.Add(1);

    public void Dispose()
    {
        _metrics.Dispose();
    }
}
