namespace KJ.Domain.Services;

public sealed class AlarmService : IAlarmService
{
    public event EventHandler<AlarmEvent>? AlarmRaised;

    public void Raise(AlarmEvent alarmEvent)
    {
        AlarmRaised?.Invoke(this, alarmEvent);
    }
}

