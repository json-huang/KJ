using System.Collections.ObjectModel;
using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Alarm.ViewModels;

public sealed class AlarmHomeViewModel : BindableBase
{
    private readonly IAlarmService _alarmService;

    public ObservableCollection<ActiveAlarmDisplay> ActiveAlarms { get; } = new();

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public DelegateCommand RefreshCommand { get; }

    public AlarmHomeViewModel(IAlarmService alarmService)
    {
        _alarmService = alarmService;
        _alarmService.AlarmRaised += (_, _) => Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Refresh);
        RefreshCommand = new DelegateCommand(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        ActiveAlarms.Clear();
        foreach (var alarm in _alarmService.GetActiveAlarms())
        {
            ActiveAlarms.Add(new ActiveAlarmDisplay
            {
                Id = alarm.Id,
                TagKey = alarm.TagKey,
                Message = alarm.Message,
                Severity = alarm.Severity.ToString(),
                TriggeredAt = alarm.TriggeredAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }
        StatusText = $"活动报警: {ActiveAlarms.Count}";
    }
}

public sealed class ActiveAlarmDisplay
{
    public string Id { get; set; } = string.Empty;
    public string TagKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string TriggeredAt { get; set; } = string.Empty;
}
