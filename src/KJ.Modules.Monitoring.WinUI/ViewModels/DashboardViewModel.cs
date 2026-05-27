using System.Collections.ObjectModel;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using KJ.Modules.Core.Regions;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class DashboardViewModel : BindableBase
{
    private readonly IAlarmService _alarmService;
    private readonly ITagStore _tagStore;
    private readonly IRegionManager _regionManager;
    private readonly IDashboardDemoDataEnsurer _demoDataEnsurer;
    private readonly IDatabaseInitSignal _databaseInitSignal;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

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
        IAlarmService alarmService,
        ITagStore tagStore,
        IRegionManager regionManager,
        IDashboardDemoDataEnsurer demoDataEnsurer,
        IDatabaseInitSignal databaseInitSignal,
        IDbContextFactory<KjDbContext> dbFactory)
    {
        _alarmService = alarmService;
        _tagStore = tagStore;
        _regionManager = regionManager;
        _demoDataEnsurer = demoDataEnsurer;
        _databaseInitSignal = databaseInitSignal;
        _dbFactory = dbFactory;

        _alarmService.AlarmRaised += (_, _) =>
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => _ = RefreshAsync());

        RefreshCommand = new DelegateCommand(() => _ = RefreshAsync());
        NavigateToAlarmsCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate(RegionNames.MainContent, new Uri("AlarmHome", UriKind.Relative)));
        NavigateToDevicesCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate(RegionNames.MainContent, new Uri("DeviceList", UriKind.Relative)));
    }

    public async Task RefreshAsync()
    {
        try
        {
            await _databaseInitSignal.WhenReadyAsync().ConfigureAwait(true);
            await _demoDataEnsurer.EnsureAsync().ConfigureAwait(true);

            await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(true);

            var devices = await db.Devices.AsNoTracking().ToListAsync().ConfigureAwait(true);
            DeviceCount = devices.Count;

            var connected = devices.Count(d => d.State == ConnectionState.Connected);
            ConnectionQuality = DeviceCount > 0 ? $"{connected}/{DeviceCount} 在线" : "无设备";

            var activeAlarms = _alarmService.GetActiveAlarms();
            ActiveAlarmCount = activeAlarms.Count;
            SystemStatus = ActiveAlarmCount > 0 ? "有报警" : "正常";

            var critical = activeAlarms.Count(a => a.Severity == AlarmSeverity.Critical);
            var warning = activeAlarms.Count(a => a.Severity == AlarmSeverity.Warning);
            var info = activeAlarms.Count(a => a.Severity == AlarmSeverity.Info);
            AlarmSeverityText = $"高 {critical} · 中 {warning} · 低 {info}";

            TagCount = _tagStore is InMemoryTagStore memoryStore ? memoryStore.Count : TagCount;

            await LoadRecentEventsAsync(db).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ConnectionQuality = "加载失败";
            SystemStatus = ex.Message.Length > 40 ? ex.Message[..40] + "…" : ex.Message;
        }
    }

    private async Task LoadRecentEventsAsync(KjDbContext db)
    {
        try
        {
            var end = DateTime.UtcNow;
            var start = end.AddHours(-24);

            var rows = await db.AuditLogs
                .AsNoTracking()
                .Where(l => l.Timestamp >= start && l.Timestamp <= end)
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .ToListAsync()
                .ConfigureAwait(true);

            RecentEvents.Clear();
            foreach (var row in rows)
            {
                var timestamp = new DateTimeOffset(row.Timestamp, TimeSpan.Zero);
                var elapsed = DateTimeOffset.UtcNow - timestamp;
                var timeText = elapsed.TotalMinutes < 1 ? "刚刚"
                    : elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes}m ago"
                    : elapsed.TotalDays < 1 ? $"{(int)elapsed.TotalHours}h ago"
                    : $"{(int)elapsed.TotalDays}d ago";

                RecentEvents.Add(new RecentEventItem
                {
                    Type = row.Action,
                    Message = row.Details ?? row.Action,
                    TimeText = timeText,
                });
            }
        }
        catch
        {
            // 审计查询失败时保持列表为空
        }
    }
}

public sealed class RecentEventItem
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
}
