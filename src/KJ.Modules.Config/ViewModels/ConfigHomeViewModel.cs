using System.Collections.ObjectModel;
using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Config.ViewModels;

public sealed class ConfigHomeViewModel : BindableBase
{
    private readonly IDeviceManager _deviceManager;

    public ObservableCollection<DeviceDisplayItem> Devices { get; } = new();

    private string _newDeviceId = string.Empty;
    public string NewDeviceId { get => _newDeviceId; set => SetProperty(ref _newDeviceId, value); }

    private string _newDeviceName = string.Empty;
    public string NewDeviceName { get => _newDeviceName; set => SetProperty(ref _newDeviceName, value); }

    private string _newDriverType = "Tcp";
    public string NewDriverType { get => _newDriverType; set => SetProperty(ref _newDriverType, value); }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand AddDeviceCommand { get; }
    public DelegateCommand<string> RemoveDeviceCommand { get; }

    public ConfigHomeViewModel(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        RefreshCommand = new DelegateCommand(() => _ = RefreshAsync());
        AddDeviceCommand = new DelegateCommand(() => _ = AddDeviceAsync());
        RemoveDeviceCommand = new DelegateCommand<string>(deviceId => _ = RemoveDeviceAsync(deviceId));
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => _ = RefreshAsync());
    }

    private async Task RemoveDeviceAsync(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        _deviceManager.RemoveDevice(deviceId);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        var devices = await Task.Run(() => _deviceManager.ListDevices()).ConfigureAwait(true);
        Devices.Clear();
        foreach (var d in devices)
        {
            Devices.Add(new DeviceDisplayItem
            {
                DeviceId = d.DeviceId,
                DisplayName = d.DisplayName,
                DriverType = d.DriverType,
                State = d.State,
            });
        }
    }

    private async Task AddDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceId) || string.IsNullOrWhiteSpace(NewDeviceName))
            return;

        try
        {
            _deviceManager.AddDevice(new DeviceDescriptor(NewDeviceId, NewDeviceName, NewDriverType));
            NewDeviceId = string.Empty;
            NewDeviceName = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            // Device already exists — ignore
        }
    }
}

public sealed class DeviceDisplayItem
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DriverType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
