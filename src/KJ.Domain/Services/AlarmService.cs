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
            if (IsTriggered(rule.Condition, value))
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

    private static bool IsTriggered(AlarmCondition condition, object? value)
    {
        if (value is null) return false;
        try
        {
            var numericValue = Convert.ToDouble(value);
            return condition switch
            {
                AlarmCondition.GreaterThan => numericValue > 0,
                AlarmCondition.LessThan => numericValue < 0,
                AlarmCondition.Equals => true,
                AlarmCondition.NotEquals => true,
                _ => false,
            };
        }
        catch
        {
            return condition == AlarmCondition.Equals;
        }
    }
}
