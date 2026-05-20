using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class DeviceManagerTests
{
    [Fact]
    public void ListDevices_ShouldReturnEmpty_WhenNoDevices()
    {
        var mgr = new DeviceManager();
        mgr.ListDevices().Should().BeEmpty();
    }

    [Fact]
    public void AddDevice_ShouldAddToList()
    {
        var mgr = new DeviceManager();
        var device = new DeviceDescriptor("dev1", "Device 1", "Tcp");

        mgr.AddDevice(device);

        mgr.ListDevices().Should().HaveCount(1);
        mgr.GetDevice("dev1")!.DisplayName.Should().Be("Device 1");
    }

    [Fact]
    public void AddDevice_ShouldThrow_WhenDuplicate()
    {
        var mgr = new DeviceManager();
        mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Tcp"));

        var act = () => mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1 Copy", "Tcp"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveDevice_ShouldRemoveFromList()
    {
        var mgr = new DeviceManager();
        mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Tcp"));

        mgr.RemoveDevice("dev1");

        mgr.ListDevices().Should().BeEmpty();
    }

    [Fact]
    public void GetDevice_ShouldReturnNull_WhenNotFound()
    {
        var mgr = new DeviceManager();
        mgr.GetDevice("nonexistent").Should().BeNull();
    }

    [Fact]
    public void UpdateDeviceState_ShouldUpdateState()
    {
        var mgr = new DeviceManager();
        mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Tcp"));

        mgr.UpdateDeviceState("dev1", "Connected");

        mgr.GetDevice("dev1")!.State.Should().Be("Connected");
    }

    [Fact]
    public void UpdateDeviceState_ShouldThrow_WhenNotFound()
    {
        var mgr = new DeviceManager();
        var act = () => mgr.UpdateDeviceState("nonexistent", "Connected");
        act.Should().Throw<InvalidOperationException>();
    }
}
