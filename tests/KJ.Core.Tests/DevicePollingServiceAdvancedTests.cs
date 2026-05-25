using FluentAssertions;
using KJ.Core;
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Drivers;
using KJ.Drivers.Abstractions;
using Xunit;

namespace KJ.Core.Tests;

public class DevicePollingServiceAdvancedTests
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

    private sealed class FakeTagConfigStore : ITagConfigStore
    {
        private readonly List<TagConfig> _tags = new();
        public void Add(TagConfig tag) => _tags.Add(tag);
        public IReadOnlyList<TagConfig> GetAllTags() => _tags.AsReadOnly();
        public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId) =>
            _tags.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }

    private sealed class FakeDriver : IDeviceDriver
    {
        public string DriverType => "Fake";
        public DriverConnectionState State { get; set; } = DriverConnectionState.Disconnected;
        public int ConnectCount { get; private set; }
        public int ReadCount { get; private set; }
        public Func<TagReadRequest, TagReadResult>? OnRead { get; set; }
        public bool ShouldFailConnect { get; set; }

        public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken ct = default)
        {
            ConnectCount++;
            if (ShouldFailConnect) throw new Exception("Connection refused");
            State = DriverConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = DriverConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken ct = default)
        {
            ReadCount++;
            if (OnRead is not null) return Task.FromResult(OnRead(request));
            return Task.FromResult(new TagReadResult(request.TagKey, 42, DateTimeOffset.Now, true));
        }

        public Task WriteAsync(TagWriteRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeDriverFactory : IDeviceDriverFactory
    {
        public FakeDriver Driver { get; set; } = new();
        public List<FakeDriver> CreatedDrivers { get; } = new();
        public IDeviceDriver Create(string driverType)
        {
            var d = new FakeDriver { OnRead = Driver.OnRead, ShouldFailConnect = Driver.ShouldFailConnect };
            CreatedDrivers.Add(d);
            return d;
        }
        public IReadOnlyList<string> GetSupportedDrivers() => new[] { "Fake" };
    }

    // ── 测试 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldSkipDevicesWithoutHost()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake")); // no host
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        factory.CreatedDrivers.Should().BeEmpty();

        await service.StopAsync();
    }

    [Fact]
    public async Task ShouldConnectDevicesWithHost()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32));
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        factory.CreatedDrivers.Should().HaveCount(1);
        deviceManager.StateUpdates.Should().Contain(("dev1", "Connected"));

        await service.StopAsync();
    }

    [Fact]
    public async Task ShouldMarkFaulted_OnConnectFailure()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "bad-host", Port: 999));
        var factory = new FakeDriverFactory();
        factory.Driver.ShouldFailConnect = true;
        var tagConfig = new FakeTagConfigStore();
        var tagStore = new InMemoryTagStore();
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag);
        await service.StartAsync();

        deviceManager.StateUpdates.Should().Contain(("dev1", "Faulted"));

        await service.StopAsync();
    }

    [Fact]
    public async Task ShouldReadTagsAndUpsert()
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

        // 等待至少一次轮询
        await Task.Delay(1500);

        tagStore.TryGet(new TagId("temp"), out var value).Should().BeTrue();
        value.Value.Should().Be(100);
        value.Quality.Should().Be(TagQuality.Good);

        await service.StopAsync();
    }

    [Fact]
    public async Task ShouldEvaluateAlarms_OnRead()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Fake", Host: "localhost", Port: 502));
        var factory = new FakeDriverFactory();
        factory.Driver.OnRead = req => new TagReadResult(req.TagKey, 150, DateTimeOffset.Now, true);
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32));
        var tagStore = new InMemoryTagStore();
        var alarmService = new AlarmService();
        alarmService.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan,
            AlarmSeverity.Warning, "High temp", true, HighThreshold: 100));
        var diag = new DiagnosticHub();

        var service = new DevicePollingService(deviceManager, factory, tagConfig, tagStore, diag, alarmService);
        await service.StartAsync();

        await Task.Delay(2000);

        // 多次轮询可能产生多个告警
        alarmService.GetActiveAlarms().Count.Should().BeGreaterThan(0);

        await service.StopAsync();
    }

    [Fact]
    public async Task ShouldDisconnect_OnStop()
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

        factory.CreatedDrivers.Should().HaveCount(1);
        factory.CreatedDrivers[0].State.Should().Be(DriverConnectionState.Disconnected);
    }
}
