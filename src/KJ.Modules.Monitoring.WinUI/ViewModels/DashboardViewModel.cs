using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class DashboardViewModel : BindableBase
{
    private readonly IDeviceManager _deviceManager;
    private readonly IAlarmService _alarmService;

    private int _deviceCount;
    public int DeviceCount { get => _deviceCount; set => SetProperty(ref _deviceCount, value); }

    private int _activeAlarmCount;
    public int ActiveAlarmCount { get => _activeAlarmCount; set => SetProperty(ref _activeAlarmCount, value); }

    private string _systemStatus = "正常";
    public string SystemStatus { get => _systemStatus; set => SetProperty(ref _systemStatus, value); }

    public DelegateCommand RefreshCommand { get; }

    public DashboardViewModel(IDeviceManager deviceManager, IAlarmService alarmService)
    {
        _deviceManager = deviceManager;
        _alarmService = alarmService;
        _alarmService.AlarmRaised += (_, _) => Refresh();
        RefreshCommand = new DelegateCommand(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        DeviceCount = _deviceManager.ListDevices().Count;
        ActiveAlarmCount = _alarmService.GetActiveAlarms().Count;
        SystemStatus = ActiveAlarmCount > 0 ? "有报警" : "正常";
    }
}
