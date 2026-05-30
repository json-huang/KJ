using System.Collections.ObjectModel;
using System.Windows.Input;
using KJ.Domain;
using KJ.Domain.Services;
using Microsoft.UI.Dispatching;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Config.ViewModels;

/// <summary>
/// 配置主页 ViewModel — 设备管理。
/// 注意：此版本不依赖 WinUI Dispatcher，可在非 UI 环境测试。
/// WinUI 版本需在构造函数中手动调用 Refresh()。
/// </summary>
public sealed class ConfigHomeViewModel : BindableBase
{
    private readonly IDeviceManager _deviceManager;
    private readonly IDeviceConnectionService _deviceConnection;
    private DispatcherQueue? _dispatcher;

    public ObservableCollection<DeviceDisplayItem> Devices { get; } = new();

    private string _actionMessage = string.Empty;
    public string ActionMessage
    {
        get => _actionMessage;
        private set => SetProperty(ref _actionMessage, value);
    }

    public string NewDeviceId { get; set; } = string.Empty;
    public string NewDeviceName { get; set; } = string.Empty;
    private string _newDriverType = "Tcp";
    public string NewDriverType
    {
        get => _newDriverType;
        set
        {
            _newDriverType = value ?? "Tcp";

            // driver 切换时给出更合理的默认 Host/Port，避免“加完设备却连不上”
            switch (_newDriverType)
            {
                case "Plc.Beckhoff.Ads":
                    if (string.IsNullOrWhiteSpace(NewHost))
                        NewHost = "127.0.0.1.1.1";
                    if (string.IsNullOrWhiteSpace(NewPortText))
                        NewPortText = "851";
                    break;
                case "OpcUa":
                    if (string.IsNullOrWhiteSpace(NewPortText))
                        NewPortText = "4840";
                    break;
                case "ModbusTcp":
                    if (string.IsNullOrWhiteSpace(NewPortText))
                        NewPortText = "502";
                    break;
            }
        }
    }

    public string NewHost { get; set; } = "";
    public string NewPortText { get; set; } = "";

    public ICommand RefreshCommand { get; }
    public ICommand AddDeviceCommand { get; }
    public ICommand RemoveDeviceCommand { get; }
    public ICommand ConnectDeviceCommand { get; }
    public ICommand DisconnectDeviceCommand { get; }

    public ConfigHomeViewModel(IDeviceManager deviceManager, IDeviceConnectionService deviceConnection)
    {
        _deviceManager = deviceManager;
        _deviceConnection = deviceConnection;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        RefreshCommand = new DelegateCommand(Refresh);
        AddDeviceCommand = new DelegateCommand(() => AddDevice());
        RemoveDeviceCommand = new DelegateCommand<string>(RemoveDevice);
        ConnectDeviceCommand = new DelegateCommand<string>(id => _ = ConnectDeviceAsync(id), id => !string.IsNullOrWhiteSpace(id));
        DisconnectDeviceCommand = new DelegateCommand<string>(id => _ = DisconnectDeviceAsync(id), id => !string.IsNullOrWhiteSpace(id));
    }

    private void SetActionMessage(string message) =>
        RunOnUiThread(() => ActionMessage = message);

    private void RunOnUiThread(Action action)
    {
        // Prism 可能在非 UI 线程构造 VM，这里允许后续从 Page.Loaded 注入 dispatcher。
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();

        if (_dispatcher is null)
        {
            action();
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _ = _dispatcher.TryEnqueue(() => action());
    }

    public void AttachDispatcher(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private async Task ConnectDeviceAsync(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        SetActionMessage($"正在连接设备 {deviceId}…");

        try
        {
            await _deviceConnection.ConnectAsync(deviceId).ConfigureAwait(true);
            var device = _deviceManager.GetDevice(deviceId);
            var state = device?.State ?? "Unknown";
            SetActionMessage(state.Equals("Connected", StringComparison.OrdinalIgnoreCase)
                ? $"连接成功：{device?.DisplayName ?? deviceId}（{device?.Host}:{device?.Port}）"
                : $"连接失败：{device?.DisplayName ?? deviceId}，状态={state}");
        }
        catch (Exception ex)
        {
            SetActionMessage($"连接失败：{deviceId} — {ex.Message}");
        }
        finally
        {
            RunOnUiThread(Refresh);
        }
    }

    private async Task DisconnectDeviceAsync(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        SetActionMessage($"正在断开设备 {deviceId}…");

        try
        {
            await _deviceConnection.DisconnectAsync(deviceId).ConfigureAwait(true);
            SetActionMessage($"已断开：{deviceId}");
        }
        catch (Exception ex)
        {
            SetActionMessage($"断开失败：{deviceId} — {ex.Message}");
        }
        finally
        {
            RunOnUiThread(Refresh);
        }
    }

    public void Refresh()
    {
        RunOnUiThread(() =>
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
        });
    }

    public bool AddDevice()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceId) || string.IsNullOrWhiteSpace(NewDeviceName))
            return false;

        var port = 0;
        if (!string.IsNullOrWhiteSpace(NewPortText) && !int.TryParse(NewPortText, out port))
            port = 0;

        try
        {
            _deviceManager.AddDevice(new DeviceDescriptor(
                NewDeviceId, NewDeviceName, NewDriverType,
                Host: NewHost, Port: port));
            NewDeviceId = string.Empty;
            NewDeviceName = string.Empty;
            NewHost = "";
            NewPortText = "";
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
