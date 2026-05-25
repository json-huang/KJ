using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Infrastructure.Services;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class ConfigImportExportTests
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

    [Fact]
    public void ExportAll_ShouldProduceValidJson()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("d1", "PLC-1", "ModbusTcp", Host: "192.168.1.1", Port: 502));
        var tagManager = new TagManager();
        tagManager.AddTag(new TagConfig(Guid.NewGuid(), "temp", "d1", "HR0", TagValueType.Int32));
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        var json = svc.ExportAll();

        json.Should().Contain("PLC-1");
        json.Should().Contain("temp");
        json.Should().Contain("192.168.1.1");
    }

    [Fact]
    public void Import_ShouldSucceed_WithValidJson()
    {
        var deviceManager = new FakeDeviceManager();
        var tagManager = new TagManager();
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        var json = @"{
            ""exportedAt"": ""2026-05-23T00:00:00+08:00"",
            ""devices"": [{""deviceId"":""d1"",""displayName"":""PLC"",""driverType"":""ModbusTcp"",""host"":""192.168.1.1"",""port"":502}],
            ""tags"": [{""tagId"":""" + Guid.NewGuid() + @""",""tagKey"":""temp"",""deviceId"":""d1"",""address"":""HR0"",""valueType"":""Int32""}]
        }";

        var result = svc.Import(json);

        result.Success.Should().BeTrue();
        result.DevicesAdded.Should().Be(1);
        result.TagsAdded.Should().Be(1);
    }

    [Fact]
    public void Import_ShouldSkipExisting_WhenOverwriteFalse()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("d1", "Existing", "Tcp"));
        var tagManager = new TagManager();
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        var json = @"{
            ""exportedAt"": ""2026-05-23T00:00:00+08:00"",
            ""devices"": [{""deviceId"":""d1"",""displayName"":""New"",""driverType"":""Tcp"",""host"":"""",""port"":0}],
            ""tags"": []
        }";

        var result = svc.Import(json, overwrite: false);

        result.Success.Should().BeTrue();
        result.DevicesAdded.Should().Be(0);
        result.Warnings.Should().Contain(w => w.Contains("already exists"));
    }

    [Fact]
    public void Import_ShouldOverwrite_WhenOverwriteTrue()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("d1", "Old", "Tcp"));
        var tagManager = new TagManager();
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        var json = @"{
            ""exportedAt"": ""2026-05-23T00:00:00+08:00"",
            ""devices"": [{""deviceId"":""d1"",""displayName"":""New"",""driverType"":""ModbusTcp"",""host"":""10.0.0.1"",""port"":502}],
            ""tags"": []
        }";

        var result = svc.Import(json, overwrite: true);

        result.Success.Should().BeTrue();
        result.DevicesAdded.Should().Be(1);
        deviceManager.GetDevice("d1")!.DisplayName.Should().Be("New");
    }

    [Fact]
    public void Import_ShouldReturnError_ForInvalidJson()
    {
        var deviceManager = new FakeDeviceManager();
        var tagManager = new TagManager();
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        var result = svc.Import("not valid json {{{");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExportImport_RoundTrip_ShouldPreserveData()
    {
        var deviceManager = new FakeDeviceManager();
        deviceManager.AddDevice(new DeviceDescriptor("d1", "PLC-1", "ModbusTcp", Host: "192.168.1.1", Port: 502));
        var tagManager = new TagManager();
        tagManager.AddTag(new TagConfig(Guid.NewGuid(), "speed", "d1", "HR0", TagValueType.Int32));
        tagManager.AddTag(new TagConfig(Guid.NewGuid(), "temp", "d1", "HR2", TagValueType.Float));
        var svc = new ConfigImportExportService(deviceManager, tagManager);

        // 导出
        var json = svc.ExportAll();

        // 导入到新的空实例
        var newDeviceManager = new FakeDeviceManager();
        var newTagManager = new TagManager();
        var newSvc = new ConfigImportExportService(newDeviceManager, newTagManager);

        var result = newSvc.Import(json);

        result.Success.Should().BeTrue();
        newDeviceManager.ListDevices().Should().HaveCount(1);
        newTagManager.GetAllTags().Should().HaveCount(2);
    }
}
