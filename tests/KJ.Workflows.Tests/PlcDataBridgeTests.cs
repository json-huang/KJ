using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Drivers.Abstractions;
using KJ.Workflows;
using Xunit;

namespace KJ.Workflows.Tests;

public class PlcDataBridgeTests
{
    private sealed class FakeDeviceManager : IDeviceManager
    {
        private readonly List<DeviceDescriptor> _devices = new();
        public void AddDevice(DeviceDescriptor device) => _devices.Add(device);
        public DeviceDescriptor? GetDevice(string deviceId) => _devices.FirstOrDefault(d => d.DeviceId == deviceId);
        public IReadOnlyList<DeviceDescriptor> ListDevices() => _devices.AsReadOnly();
        public void RemoveDevice(string deviceId) => _devices.RemoveAll(d => d.DeviceId == deviceId);
        public void UpdateDeviceState(string deviceId, string state) { }
    }

    private sealed class FakeTagConfigStore : ITagConfigStore
    {
        private readonly List<TagConfig> _tags = new();
        public void Add(TagConfig tag) => _tags.Add(tag);
        public IReadOnlyList<TagConfig> GetAllTags() => _tags.AsReadOnly();
        public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId) =>
            _tags.Where(t => t.DeviceId == deviceId).ToList().AsReadOnly();
    }

    private sealed class FakeDriverFactory : IDeviceDriverFactory
    {
        public FakeDriver Driver { get; } = new();
        public IDeviceDriver Create(string driverType) => Driver;
        public IReadOnlyList<string> GetSupportedDrivers() => new[] { "Fake" };
    }

    private sealed class FakeDriver : IDeviceDriver
    {
        public string DriverType => "Fake";
        public DriverConnectionState State => DriverConnectionState.Connected;
        public Func<TagReadRequest, TagReadResult>? OnRead { get; set; }
        public Action<TagWriteRequest>? OnWrite { get; set; }

        public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken ct = default)
        {
            if (OnRead is not null) return Task.FromResult(OnRead(request));
            return Task.FromResult(new TagReadResult(request.TagKey, 42, DateTimeOffset.Now, true));
        }

        public Task WriteAsync(TagWriteRequest request, CancellationToken ct = default)
        {
            OnWrite?.Invoke(request);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ── ReadSignalAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ReadSignal_ShouldSucceed_WhenDeviceExists()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake", Host: "localhost"));
        var driverFactory = new FakeDriverFactory();
        driverFactory.Driver.OnRead = req => new TagReadResult(req.TagKey, 100, DateTimeOffset.Now, true);
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var result = await bridge.ReadSignalAsync("plc1", "MAIN.nSpeed", TagValueType.Int32);

        result.Success.Should().BeTrue();
        result.Value.Should().Be(100);
    }

    [Fact]
    public async Task ReadSignal_ShouldSyncToTagStore()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake", Host: "localhost"));
        var driverFactory = new FakeDriverFactory();
        driverFactory.Driver.OnRead = req => new TagReadResult(req.TagKey, 42, DateTimeOffset.Now, true);
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        await bridge.ReadSignalAsync("plc1", "MAIN.nSpeed", TagValueType.Int32);

        tagStore.TryGet(new TagId("MAIN.nSpeed"), out var value).Should().BeTrue();
        value.Value.Should().Be(42);
        value.Quality.Should().Be(TagQuality.Good);
    }

    [Fact]
    public async Task ReadSignal_ShouldFail_WhenDeviceNotFound()
    {
        var deviceManager = new FakeDeviceManager();
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var result = await bridge.ReadSignalAsync("nonexistent", "addr", TagValueType.Int32);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ReadSignal_ShouldFail_WhenDriverFails()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake", Host: "localhost"));
        var driverFactory = new FakeDriverFactory();
        driverFactory.Driver.OnRead = req => new TagReadResult(req.TagKey, null, DateTimeOffset.Now, false, "read error");
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var result = await bridge.ReadSignalAsync("plc1", "MAIN.nSpeed", TagValueType.Int32);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("read error");
    }

    // ── WriteSignalAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task WriteSignal_ShouldSucceed()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake", Host: "localhost"));
        var driverFactory = new FakeDriverFactory();
        TagWriteRequest? written = null;
        driverFactory.Driver.OnWrite = req => written = req;
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var result = await bridge.WriteSignalAsync("plc1", "GVL.bRun", TagValueType.Bool, true);

        result.Success.Should().BeTrue();
        written.Should().NotBeNull();
        written!.Value.Should().Be(true);
    }

    [Fact]
    public async Task WriteSignal_ShouldSyncToTagStore()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake", Host: "localhost"));
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        await bridge.WriteSignalAsync("plc1", "GVL.bRun", TagValueType.Bool, true);

        tagStore.TryGet(new TagId("GVL.bRun"), out var value).Should().BeTrue();
        value.Value.Should().Be(true);
    }

    // ── GetCachedValue ───────────────────────────────────────────────────

    [Fact]
    public void GetCachedValue_ShouldReturnNull_WhenNotCached()
    {
        var deviceManager = new FakeDeviceManager();
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        bridge.GetCachedValue("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetCachedValue_ShouldReturnValue_WhenCached()
    {
        var deviceManager = new FakeDeviceManager();
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        tagStore.Upsert(new TagValue(new TagId("temp"), 42, TagQuality.Good, DateTimeOffset.Now));
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        bridge.GetCachedValue("temp").Should().Be(42);
    }

    // ── BrowseTags / BrowseDevices ───────────────────────────────────────

    [Fact]
    public void BrowseTags_ShouldReturnConfiguredTags()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC", "Fake"));
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "speed", "plc1", "MAIN.nSpeed", TagValueType.Int32));
        tagConfig.Add(new TagConfig(Guid.NewGuid(), "temp", "plc1", "MAIN.nTemp", TagValueType.Float));
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var tags = bridge.BrowseTags("plc1");

        tags.Should().HaveCount(2);
        tags.Should().Contain(t => t.TagKey == "speed" && t.Address == "MAIN.nSpeed");
    }

    [Fact]
    public void BrowseDevices_ShouldReturnRegisteredDevices()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC-1", "ModbusTcp"));
        deviceManager.AddDevice(new DeviceDescriptor("plc2", "PLC-2", "OpcUa"));
        var driverFactory = new FakeDriverFactory();
        var tagStore = new InMemoryTagStore();
        var tagConfig = new FakeTagConfigStore();
        var bridge = new PlcDataBridge(driverFactory, deviceManager, tagStore, tagConfig);

        var devices = bridge.BrowseDevices();

        devices.Should().HaveCount(2);
        devices.Should().Contain(d => d.DeviceId == "plc1" && d.DisplayName == "PLC-1");
    }

    // ── ParsePlcType ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("BOOL", TagValueType.Bool)]
    [InlineData("DINT", TagValueType.Int32)]
    [InlineData("REAL", TagValueType.Float)]
    [InlineData("LREAL", TagValueType.Double)]
    [InlineData("STRING", TagValueType.String)]
    [InlineData("BYTE", TagValueType.Bytes)]
    public void ParsePlcType_ShouldMapCorrectly(string plcType, TagValueType expected)
    {
        PlcDataBridge.ParsePlcType(plcType).Should().Be(expected);
    }

    [Fact]
    public void ParsePlcType_ShouldDefaultToInt32_ForUnknown()
    {
        PlcDataBridge.ParsePlcType("UNKNOWN_TYPE").Should().Be(TagValueType.Int32);
    }

    // ── ConvertValue ─────────────────────────────────────────────────────

    [Fact]
    public void ConvertValue_ShouldConvert_Bool()
    {
        PlcDataBridge.ConvertValue("true", TagValueType.Bool).Should().Be(true);
        PlcDataBridge.ConvertValue("0", TagValueType.Bool).Should().Be(false);
    }

    [Fact]
    public void ConvertValue_ShouldConvert_Int32()
    {
        PlcDataBridge.ConvertValue("42", TagValueType.Int32).Should().Be(42);
    }

    [Fact]
    public void ConvertValue_ShouldConvert_Float()
    {
        ((float)PlcDataBridge.ConvertValue("3.14", TagValueType.Float)!).Should().BeApproximately(3.14f, 0.01f);
    }

    [Fact]
    public void ConvertValue_ShouldReturnNull_ForNullInput()
    {
        PlcDataBridge.ConvertValue(null, TagValueType.Int32).Should().BeNull();
    }
}
