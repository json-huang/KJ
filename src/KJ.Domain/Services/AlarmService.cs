using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class AlarmService : IAlarmService
{
    public event EventHandler<AlarmEvent>? AlarmRaised;

    private readonly ConcurrentDictionary<string, AlarmRule> _rules = new();
    private readonly ConcurrentDictionary<string, ActiveAlarm> _activeAlarms = new();

    public void Raise(AlarmEvent alarmEvent)
    {
        AlarmRaised?.Invoke(this, alarmEvent);
    }

    public void AddRule(AlarmRule rule) =>
        _rules.TryAdd(rule.Id, rule);

    public void RemoveRule(string ruleId) =>
        _rules.TryRemove(ruleId, out _);

    public IReadOnlyList<AlarmRule> GetRules() =>
        _rules.Values.ToList().AsReadOnly();

    public IReadOnlyList<ActiveAlarm> GetActiveAlarms() =>
        _activeAlarms.Values.Where(a => !a.Acknowledged).ToList().AsReadOnly();

    /// <summary>无活动告警时注入演示数据（仅本地演示，可重复调用）。</summary>
    public void EnsureDemoActiveAlarmsIfEmpty()
    {
        if (_activeAlarms.Values.Any(a => !a.Acknowledged))
            return;

        var now = DateTimeOffset.UtcNow;
        var demos = new[]
        {
            ("demo-alarm-01", "rule-temp-high", "LineA.Temp", "1号线反应釜温度超过设定上限", AlarmSeverity.Critical, now.AddMinutes(-18)),
            ("demo-alarm-02", "rule-pressure", "Mixer02.Pressure", "混合罐压力偏高", AlarmSeverity.Warning, now.AddMinutes(-42)),
            ("demo-alarm-03", "rule-comm", "OpcGateway.Link", "OPC 网关通讯抖动", AlarmSeverity.Info, now.AddMinutes(-95)),
        };

        foreach (var (id, ruleId, tagKey, message, severity, triggeredAt) in demos)
        {
            _activeAlarms.TryAdd(
                id,
                new ActiveAlarm(id, ruleId, tagKey, message, severity, triggeredAt, false, null));
        }
    }

    public void AcknowledgeAlarm(string alarmId, string userId)
    {
        _activeAlarms.AddOrUpdate(alarmId,
            _ => throw new InvalidOperationException($"Alarm '{alarmId}' not found."),
            (_, existing) => existing with { Acknowledged = true, AcknowledgedBy = userId });
    }

    public void ClearAlarm(string alarmId) =>
        _activeAlarms.TryRemove(alarmId, out _);

    public void Evaluate(string tagKey, object? value)
    {
        foreach (var rule in _rules.Values.Where(r => r.IsEnabled && r.TagKey == tagKey))
        {
            if (IsTriggered(rule.Condition, value, rule.HighThreshold, rule.LowThreshold))
            {
                var alarmId = $"{rule.Id}_{DateTimeOffset.UtcNow.Ticks}";
                var alarm = new ActiveAlarm(
                    alarmId, rule.Id, tagKey, rule.Message,
                    rule.Severity, DateTimeOffset.UtcNow, false, null);
                _activeAlarms.TryAdd(alarmId, alarm);

                Raise(new AlarmEvent(rule.Id, rule.Message, rule.Severity, DateTimeOffset.UtcNow));
            }
        }
    }

    private static bool IsTriggered(AlarmCondition condition, object? value, double highThreshold, double lowThreshold)
    {
        if (value is null) return false;
        try
        {
            var numericValue = Convert.ToDouble(value);
            return condition switch
            {
                AlarmCondition.GreaterThan => numericValue > highThreshold,
                AlarmCondition.LessThan => numericValue < lowThreshold,
                AlarmCondition.Equals => Math.Abs(numericValue - highThreshold) < 0.0001,
                AlarmCondition.NotEquals => Math.Abs(numericValue - highThreshold) >= 0.0001,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }
}
