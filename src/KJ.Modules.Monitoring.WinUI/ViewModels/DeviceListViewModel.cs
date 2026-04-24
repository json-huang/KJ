using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using KJ.Modules.Monitoring.Models;
using KJ.Modules.Monitoring.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Prism.Commands;
using Prism.Mvvm;
using Windows.UI;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class DeviceListViewModel : BindableBase
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDeviceStatusProvider _statusProvider;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private readonly List<DeviceListItem> _allItems = [];

    public ObservableCollection<DeviceListItem> Items { get; } = [];

    public bool HasItems => Items.Count > 0;
    public bool ShowEmptyState => !IsLoading && !HasError && !HasItems;

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (!SetProperty(ref _filterText, value))
                return;

            ApplyFilter();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
                RaisePropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public DelegateCommand RefreshCommand { get; }

    public DeviceListViewModel(IServiceScopeFactory serviceScopeFactory, IDeviceStatusProvider statusProvider)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _statusProvider = statusProvider;

        RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsLoading);
    }

    public async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0))
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KjDbContext>();

            var devices = await db.Devices
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();

            _allItems.Clear();
            _allItems.AddRange(devices.Select(Map));

#if DEBUG
            // Dev-only: when DB is empty, show a few rows so the UI polish is visible.
            if (_allItems.Count == 0)
            {
                _allItems.AddRange(GetSampleItems());
            }
#endif

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }

    private DeviceListItem Map(Device device)
    {
        var (state, lastSeenUtc) = ResolveStatus(device);
        var stateText = state.ToString();

        return new DeviceListItem
        {
            Id = device.Id,
            Name = device.Name,
            Type = device.Type.ToString(),
            Endpoint = $"{device.Address.Host}:{device.Address.Port}",
            StateText = stateText,
            StateBrush = new SolidColorBrush(StateToColor(state)),
            LastConnectedText = FormatLastSeen(device.LastConnected, lastSeenUtc),
        };
    }

    private (ConnectionState State, DateTimeOffset? LastSeenUtc) ResolveStatus(Device device)
    {
        if (_statusProvider.TryGet(device.Id, out var snapshot))
            return (snapshot.State, snapshot.LastSeenUtc);

        return (device.State, null);
    }

    private static Color StateToColor(ConnectionState state) =>
        state switch
        {
            ConnectionState.Connected => Colors.LimeGreen,
            ConnectionState.Connecting => Colors.Orange,
            ConnectionState.Faulted => Colors.IndianRed,
            _ => Colors.DimGray,
        };

    private static string FormatLastSeen(DateTime lastConnectedLocal, DateTimeOffset? lastSeenUtc)
    {
        if (lastSeenUtc is { } seenUtc)
            return seenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (lastConnectedLocal == default)
            return "-";

        return lastConnectedLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void ApplyFilter()
    {
        Items.Clear();

        var term = (FilterText ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            foreach (var item in _allItems)
                Items.Add(item);
            RaisePropertyChanged(nameof(HasItems));
            RaisePropertyChanged(nameof(ShowEmptyState));
            return;
        }

        var filtered = _allItems.Where(i =>
            i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.Type.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.Endpoint.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.StateText.Contains(term, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
            Items.Add(item);

        RaisePropertyChanged(nameof(HasItems));
        RaisePropertyChanged(nameof(ShowEmptyState));
    }

#if DEBUG
    private static IEnumerable<DeviceListItem> GetSampleItems()
    {
        var now = DateTimeOffset.Now;
        return new[]
        {
            new DeviceListItem
            {
                Id = Guid.NewGuid(),
                Name = "Edge-PLC-01",
                Type = "Plc",
                Endpoint = "10.0.0.12:502",
                StateText = ConnectionState.Connected.ToString(),
                StateBrush = new SolidColorBrush(Colors.LimeGreen),
                LastConnectedText = now.AddMinutes(-2).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            },
            new DeviceListItem
            {
                Id = Guid.NewGuid(),
                Name = "Mixer-02",
                Type = "Robot",
                Endpoint = "10.0.0.21:9000",
                StateText = ConnectionState.Connecting.ToString(),
                StateBrush = new SolidColorBrush(Colors.Orange),
                LastConnectedText = now.AddSeconds(-35).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            },
            new DeviceListItem
            {
                Id = Guid.NewGuid(),
                Name = "Flow-Sensor-A",
                Type = "Sensor",
                Endpoint = "10.0.0.44:1502",
                StateText = ConnectionState.Faulted.ToString(),
                StateBrush = new SolidColorBrush(Colors.IndianRed),
                LastConnectedText = "-",
            },
            new DeviceListItem
            {
                Id = Guid.NewGuid(),
                Name = "Tank-Level",
                Type = "Instrument",
                Endpoint = "10.0.0.77:10502",
                StateText = ConnectionState.Disconnected.ToString(),
                StateBrush = new SolidColorBrush(Colors.DimGray),
                LastConnectedText = now.AddHours(-6).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            },
        };
    }
#endif
}

