using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

public sealed class EfAlarmService : IAlarmService
{
    private readonly IAlarmService _inner;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly ILogger<EfAlarmService>? _logger;
    private bool _loaded;

    public event EventHandler<AlarmEvent>? AlarmRaised
    {
        add => _inner.AlarmRaised += value;
        remove => _inner.AlarmRaised -= value;
    }

    public EfAlarmService(IAlarmService inner, IDbContextFactory<KjDbContext> dbFactory, ILogger<EfAlarmService>? logger = null)
    {
        _inner = inner;
        _dbFactory = dbFactory;
        _logger = logger;
        _inner.AlarmRaised += OnAlarmRaised;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            foreach (var alarm in db.Alarms.AsNoTracking().Where(a => a.IsEnabled).ToList())
            {
                var rule = new AlarmRule(
                    alarm.Id.ToString(),
                    alarm.TagId.ToString(),
                    (KJ.Domain.AlarmCondition)(int)alarm.Condition,
                    (AlarmSeverity)(int)alarm.Level,
                    alarm.Name,
                    alarm.IsEnabled);
                try { _inner.AddRule(rule); }
                catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
    }

    private void OnAlarmRaised(object? sender, AlarmEvent e)
    {
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                db.AlarmHistory.Add(new AlarmHistory
                {
                    Id = Guid.NewGuid(),
                    AlarmId = Guid.TryParse(e.Code, out var g) ? g : Guid.Empty,
                    Timestamp = e.Timestamp.UtcDateTime,
                    EventType = "Triggered",
                    Message = e.Message,
                });
                db.SaveChanges();
            }
            catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
        });
    }

    public void Raise(AlarmEvent alarmEvent) => _inner.Raise(alarmEvent);

    public void AddRule(AlarmRule rule)
    {
        _inner.AddRule(rule);
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                var entity = new Alarm
                {
                    Id = Guid.TryParse(rule.Id, out var g) ? g : Guid.NewGuid(),
                    TagId = Guid.TryParse(rule.TagKey, out var tg) ? tg : Guid.Empty,
                    Name = rule.Message,
                    Condition = (KJ.Infrastructure.Data.Entities.AlarmCondition)(int)rule.Condition,
                    Level = (AlarmLevel)(int)rule.Severity,
                    IsEnabled = rule.IsEnabled,
                };
                db.Alarms.Add(entity);
                db.SaveChanges();
            }
            catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
        });
    }

    public void RemoveRule(string ruleId)
    {
        _inner.RemoveRule(ruleId);
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                if (Guid.TryParse(ruleId, out var guid))
                {
                    var entity = db.Alarms.Find(guid);
                    if (entity is not null)
                    {
                        db.Alarms.Remove(entity);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
        });
    }

    public IReadOnlyList<AlarmRule> GetRules()
    {
        EnsureLoaded();
        return _inner.GetRules();
    }

    public IReadOnlyList<ActiveAlarm> GetActiveAlarms()
    {
        EnsureLoaded();
        return _inner.GetActiveAlarms();
    }

    public void AcknowledgeAlarm(string alarmId, string userId)
    {
        _inner.AcknowledgeAlarm(alarmId, userId);
        _ = Task.Run(() =>
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                db.AlarmHistory.Add(new AlarmHistory
                {
                    Id = Guid.NewGuid(),
                    AlarmId = Guid.TryParse(alarmId, out var g) ? g : Guid.Empty,
                    Timestamp = DateTime.UtcNow,
                    EventType = "Acknowledged",
                    UserId = userId,
                });
                db.SaveChanges();
            }
            catch (Exception ex) { _logger?.LogWarning(ex, "Database operation failed"); }
        });
    }

    public void ClearAlarm(string alarmId) => _inner.ClearAlarm(alarmId);

    public void Evaluate(string tagKey, object? value) => _inner.Evaluate(tagKey, value);
}
