using KJ.Domain.Services;
using FluentAssertions;
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using Xunit;

namespace KJ.Core.Tests;

public class DevicePollingServiceTests
{
    private sealed class FakeDeviceManager : IDeviceManager
    {
        private readonly List<DeviceDescriptor> _devices = new();
        public List<(string DeviceId, string State)> StateUpdates { get; } = new();

        public void AddDevice(DeviceDescriptor device) => _devices.Add(device);
        public DeviceDescriptor? GetDevice(string deviceId) => _devices.FirstOrDefault(d => d.DeviceId == deviceId);
        public IReadOnlyList<DeviceDescriptor> ListDevices() => _devices.AsReadOnly();
        public void RemoveDevice(string deviceId) => _devices.RemoveAll(d => d.DeviceId == deviceId);
        public void UpdateDeviceState(string deviceId, string state) => StateUpdates.Add((deviceId, state));
    }

    private sealed class FakeDriver : IDeviceDriver
    {
        public string DriverType => "Fake";
        public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;
        public List<TagReadRequest> ReadRequests { get; } = new();
        public Func<TagReadRequest, TagReadResult>? OnRead { get; set; }
        public bool Connected { get; private set; }

        public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken ct = default)
        {
            State = DriverConnectionState.Connected;
            Connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = DriverConnectionState.Disconnected;
            Connected = false;
            return Task.CompletedTask;
        }

        public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken ct = default)
        {
            ReadRequests.Add(request);
            if (OnRead is not null)
                return Task.FromResult(OnRead(request));
            return Task.FromResult(new TagReadResult(request.TagKey, 42, DateTimeOffset.Now, true));
        }

        public Task WriteAsync(TagWriteRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeDriverFactory : IDeviceDriverFactory
    {
        public FakeDriver Driver { get; } = new();
        public IDeviceDriver Create(string driverType) => Driver;
        public IReadOnlyList<string> GetSupportedDrivers() => new[] { "Fake" };
    }

    private sealed class FakeTagConfigStore : ITagConfigStore
    {
        private readonly List<TagConfig> _tags = new();
        public void Add(TagConfig tag) => _tags.Add(tag);
        public IReadOnlyList<TagConfig> GetAllTags() => _tags.AsReadOnly();
        public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId) =>
            _tags.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ShouldConnectDevices()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "192.168.1.1", Port: 502));
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32));
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        factory.Driver.Connected.Should().BeTrue();
        deviceManager.StateUpdates.Should().Contain(("dev1", "Connected"));

        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldSkipDevicesWithoutHost()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake")); // no host
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        factory.Driver.Connected.Should().BeFalse();

        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldMarkDeviceFaulted_OnConnectFailure()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "bad-host", Port: 999));
        var factory = new FakeDriverFactory();
        // Override driver to throw on connect
        var throwingFactory = new ThrowingDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, throwingFactory, tagConfig, tagStore, diag);
        await service.StartAsync();

        deviceManager.StateUpdates.Should().Contain(("dev1", "Faulted"));

        await service.StopAsync();
    }

    [Fact]
    public async Task PollingLoop_ShouldReadTagsAndUpsertToStore()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        factory.Driver.OnRead = req => new TagReadResult(req.TagKey, 100, DateTimeOffset.Now, true);
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32));
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        // Wait for at least one poll cycle
        await Task.Delay(1500);

        tagStore.TryGet(new TagId("temp"), out var value).Should().BeTrue();
        value.Value.Should().Be(100);
        value.Quality.Should().Be(TagQuality.Good);

        await service.StopAsync();
    }

    [Fact]
    public async Task PollingLoop_ShouldSetBadQuality_OnReadFailure()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        factory.Driver.OnRead = req => new TagReadResult(req.TagKey, null, DateTimeOffset.Now, false, "read error");
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32));
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        await Task.Delay(1500);

        tagStore.TryGet(new TagId("temp"), out var value).Should().BeTrue();
        value.Quality.Should().Be(TagQuality.Bad);

        await service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldDisconnectDrivers()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();
        await service.StopAsync();

        factory.Driver.Connected.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_ShouldStopService()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();
        await service.DisposeAsync();

        factory.Driver.Connected.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private sealed class ThrowingDriverFactory : IDeviceDriverFactory
    {
        public IDeviceDriver Create(string driverType) => new ThrowingDriver();
        public IReadOnlyList<string> GetSupportedDrivers() => new[] { "Fake" };
    }

    private sealed class ThrowingDriver : IDeviceDriver
    {
        public string DriverType => "Fake";
        public DriverConnectionState State => DriverConnectionState.Disconnected;
        public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken ct = default) =>
            throw new Exception("Connection refused");
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken ct = default) =>
            Task.FromResult(new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false));
        public Task WriteAsync(TagWriteRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
