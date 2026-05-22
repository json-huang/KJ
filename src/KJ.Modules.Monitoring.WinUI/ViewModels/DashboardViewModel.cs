using System.Collections.ObjectModel;
using KJ.Domain;
using KJ.Modules.Core.Regions;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class DashboardViewModel : BindableBase
{
    private readonly IDeviceManager _deviceManager;
    private readonly IAlarmService _alarmService;
    private readonly ITagStore _tagStore;
    private readonly IAuditLogger _auditLogger;
    private readonly IRegionManager _regionManager;

    private int _deviceCount;
    public int DeviceCount { get => _deviceCount; set => SetProperty(ref _deviceCount, value); }

    private int _activeAlarmCount;
    public int ActiveAlarmCount { get => _activeAlarmCount; set => SetProperty(ref _activeAlarmCount, value); }

    private string _systemStatus = "正常";
    public string SystemStatus { get => _systemStatus; set => SetProperty(ref _systemStatus, value); }

    private string _alarmSeverityText = "高 0 · 中 0 · 低 0";
    public string AlarmSeverityText { get => _alarmSeverityText; set => SetProperty(ref _alarmSeverityText, value); }

    private int _tagCount;
    public int TagCount { get => _tagCount; set => SetProperty(ref _tagCount, value); }

    private string _connectionQuality = "--";
    public string ConnectionQuality { get => _connectionQuality; set => SetProperty(ref _connectionQuality, value); }

    public ObservableCollection<RecentEventItem> RecentEvents { get; } = new();

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand NavigateToAlarmsCommand { get; }
    public DelegateCommand NavigateToDevicesCommand { get; }

    public DashboardViewModel(
        IDeviceManager deviceManager,
        IAlarmService alarmService,
        ITagStore tagStore,
        IAuditLogger auditLogger,
        IRegionManager regionManager)
    {
        _deviceManager = deviceManager;
        _alarmService = alarmService;
        _tagStore = tagStore;
        _auditLogger = auditLogger;
        _regionManager = regionManager;

        _alarmService.AlarmRaised += (_, _) => Refresh();
        _tagStore.TagUpdated += (_, _) => TagCount++;

        RefreshCommand = new DelegateCommand(Refresh);
        NavigateToAlarmsCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate(RegionNames.MainContent, new Uri("AlarmHome", UriKind.Relative)));
        NavigateToDevicesCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate(RegionNames.MainContent, new Uri("DeviceList", UriKind.Relative)));

        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => Refresh());
    }

    private void Refresh()
    {
        DeviceCount = _deviceManager.ListDevices().Count;
        var activeAlarms = _alarmService.GetActiveAlarms();
        ActiveAlarmCount = activeAlarms.Count;
        SystemStatus = ActiveAlarmCount > 0 ? "有报警" : "正常";

        var critical = activeAlarms.Count(a => a.Severity == AlarmSeverity.Critical);
        var warning = activeAlarms.Count(a => a.Severity == AlarmSeverity.Warning);
        var info = activeAlarms.Count(a => a.Severity == AlarmSeverity.Info);
        AlarmSeverityText = $"高 {critical} · 中 {warning} · 低 {info}";

        var connected = _deviceManager.ListDevices().Count(d => d.State == "Connected");
        ConnectionQuality = DeviceCount > 0 ? $"{connected}/{DeviceCount} 在线" : "无设备";

        _ = LoadRecentEventsAsync();
    }

    private async Task LoadRecentEventsAsync()
    {
        try
        {
            var end = DateTimeOffset.UtcNow;
            var start = end.AddHours(-24);
            var logs = await _auditLogger.GetLogsAsync(start, end).ConfigureAwait(false);

            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                RecentEvents.Clear();
                foreach (var entry in logs.Take(10))
                {
                    var elapsed = DateTimeOffset.UtcNow - entry.Timestamp;
                    var timeText = elapsed.TotalMinutes < 1 ? "刚刚"
                        : elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes}m ago"
                        : elapsed.TotalDays < 1 ? $"{(int)elapsed.TotalHours}h ago"
                        : $"{(int)elapsed.TotalDays}d ago";

                    RecentEvents.Add(new RecentEventItem
                    {
                        Type = entry.Action,
                        Message = entry.Details ?? entry.Action,
                        TimeText = timeText,
                    });
                }
            });
        }
        catch { }
    }
}

public sealed class RecentEventItem
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
}
