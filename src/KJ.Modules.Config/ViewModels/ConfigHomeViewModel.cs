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

    public ConfigHomeViewModel(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        RefreshCommand = new DelegateCommand(Refresh);
        AddDeviceCommand = new DelegateCommand(AddDevice);
        Refresh();
    }

    private void Refresh()
    {
        Devices.Clear();
        foreach (var d in _deviceManager.ListDevices())
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

    private void AddDevice()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceId) || string.IsNullOrWhiteSpace(NewDeviceName))
            return;

        try
        {
            _deviceManager.AddDevice(new DeviceDescriptor(NewDeviceId, NewDeviceName, NewDriverType));
            NewDeviceId = string.Empty;
            NewDeviceName = string.Empty;
            Refresh();
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
