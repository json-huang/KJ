using System.Collections.ObjectModel;
using System.Windows.Input;
using KJ.Domain;
using KJ.Domain.Services;
using Prism.Commands;

namespace KJ.Modules.Config.ViewModels;

/// <summary>
/// 配置主页 ViewModel — 设备管理。
/// 注意：此版本不依赖 WinUI Dispatcher，可在非 UI 环境测试。
/// WinUI 版本需在构造函数中手动调用 Refresh()。
/// </summary>
public sealed class ConfigHomeViewModel
{
    private readonly IDeviceManager _deviceManager;

    public ObservableCollection<DeviceDisplayItem> Devices { get; } = new();

    public string NewDeviceId { get; set; } = string.Empty;
    public string NewDeviceName { get; set; } = string.Empty;
    public string NewDriverType { get; set; } = "Tcp";
    public string NewHost { get; set; } = "";
    public int NewPort { get; set; }

    public ICommand RefreshCommand { get; }
    public ICommand AddDeviceCommand { get; }
    public ICommand RemoveDeviceCommand { get; }

    public ConfigHomeViewModel(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        RefreshCommand = new DelegateCommand(Refresh);
        AddDeviceCommand = new DelegateCommand(() => AddDevice());
        RemoveDeviceCommand = new DelegateCommand<string>(RemoveDevice);
    }

    public void Refresh()
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
                Host = d.Host,
                Port = d.Port,
            });
        }
    }

    public bool AddDevice()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceId) || string.IsNullOrWhiteSpace(NewDeviceName))
            return false;

        try
        {
            _deviceManager.AddDevice(new DeviceDescriptor(
                NewDeviceId, NewDeviceName, NewDriverType,
                Host: NewHost, Port: NewPort));
            NewDeviceId = string.Empty;
            NewDeviceName = string.Empty;
            NewHost = "";
            NewPort = 0;
            Refresh();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void RemoveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        _deviceManager.RemoveDevice(deviceId);
        Refresh();
    }
}

/// <summary>
/// 标签管理 ViewModel。
/// </summary>
public sealed class TagConfigViewModel
{
    private readonly TagManager _tagManager;
    private readonly IDeviceManager _deviceManager;

    public ObservableCollection<TagDisplayItem> Tags { get; } = new();
    public ObservableCollection<DeviceDisplayItem> AvailableDevices { get; } = new();

    public string NewTagKey { get; set; } = "";
    public string NewDeviceId { get; set; } = "";
    public string NewAddress { get; set; } = "";
    public string NewValueType { get; set; } = "Int32";

    public TagConfigViewModel(TagManager tagManager, IDeviceManager deviceManager)
    {
        _tagManager = tagManager;
        _deviceManager = deviceManager;
    }

    public void Refresh()
    {
        Tags.Clear();
        foreach (var t in _tagManager.GetAllTags())
        {
            Tags.Add(new TagDisplayItem
            {
                TagId = t.TagId,
                TagKey = t.TagKey,
                DeviceId = t.DeviceId,
                Address = t.Address,
                ValueType = t.ValueType.ToString(),
            });
        }

        AvailableDevices.Clear();
        foreach (var d in _deviceManager.ListDevices())
        {
            AvailableDevices.Add(new DeviceDisplayItem
            {
                DeviceId = d.DeviceId,
                DisplayName = d.DisplayName,
                DriverType = d.DriverType,
            });
        }
    }

    public bool AddTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagKey) || string.IsNullOrWhiteSpace(NewDeviceId))
            return false;

        if (!Enum.TryParse<TagValueType>(NewValueType, out var valueType))
            valueType = TagValueType.Int32;

        try
        {
            _tagManager.AddTag(new TagConfig(
                TagId: Guid.NewGuid(),
                TagKey: NewTagKey,
                DeviceId: NewDeviceId,
                Address: NewAddress,
                ValueType: valueType));
            NewTagKey = "";
            NewAddress = "";
            Refresh();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void RemoveTag(Guid tagId)
    {
        _tagManager.RemoveTag(tagId);
        Refresh();
    }
}

public sealed class DeviceDisplayItem
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DriverType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
}

public sealed class TagDisplayItem
{
    public Guid TagId { get; set; }
    public string TagKey { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
}
