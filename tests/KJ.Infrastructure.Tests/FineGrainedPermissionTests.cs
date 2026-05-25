using FluentAssertions;
using KJ.Infrastructure.Auth;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class FineGrainedPermissionTests
{
    [Fact]
    public void GrantDeviceAccess_ShouldReturnTrue_WhenNewPermission()
    {
        var svc = new FineGrainedPermissionService();

        var result = svc.GrantDeviceAccess("user1", "device1");

        result.Should().BeTrue();
    }

    [Fact]
    public void GrantDeviceAccess_ShouldReturnFalse_WhenAlreadyGranted()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        var result = svc.GrantDeviceAccess("user1", "device1");

        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessDevice_ShouldReturnTrue_WhenAccessGranted()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        svc.CanAccessDevice("user1", "device1").Should().BeTrue();
    }

    [Fact]
    public void CanAccessDevice_ShouldReturnFalse_WhenNoAccess()
    {
        var svc = new FineGrainedPermissionService();

        svc.CanAccessDevice("user1", "device1").Should().BeFalse();
    }

    [Fact]
    public void CanAccessDevice_ShouldReturnFalse_ForDifferentDevice()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        svc.CanAccessDevice("user1", "device2").Should().BeFalse();
    }

    [Fact]
    public void CanAccessDevice_ShouldReturnFalse_ForDifferentUser()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        svc.CanAccessDevice("user2", "device1").Should().BeFalse();
    }

    [Fact]
    public void RevokeDeviceAccess_ShouldReturnTrue_WhenPermissionExists()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        var result = svc.RevokeDeviceAccess("user1", "device1");

        result.Should().BeTrue();
    }

    [Fact]
    public void RevokeDeviceAccess_ShouldReturnFalse_WhenNoPermission()
    {
        var svc = new FineGrainedPermissionService();

        var result = svc.RevokeDeviceAccess("user1", "device1");

        result.Should().BeFalse();
    }

    [Fact]
    public void RevokeDeviceAccess_ShouldRemoveAccess()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");

        svc.RevokeDeviceAccess("user1", "device1");

        svc.CanAccessDevice("user1", "device1").Should().BeFalse();
    }

    [Fact]
    public void GetUserDevicePermissions_ShouldReturnEmpty_WhenNoGrants()
    {
        var svc = new FineGrainedPermissionService();

        var devices = svc.GetUserDevicePermissions("user1");

        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetUserDevicePermissions_ShouldReturnGrantedDevices()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");
        svc.GrantDeviceAccess("user1", "device2");
        svc.GrantDeviceAccess("user1", "device3");

        var devices = svc.GetUserDevicePermissions("user1");

        devices.Should().HaveCount(3);
        devices.Should().Contain("device1");
        devices.Should().Contain("device2");
        devices.Should().Contain("device3");
    }

    [Fact]
    public void GetUserDevicePermissions_ShouldNotIncludeRevokedDevices()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");
        svc.GrantDeviceAccess("user1", "device2");
        svc.RevokeDeviceAccess("user1", "device1");

        var devices = svc.GetUserDevicePermissions("user1");

        devices.Should().ContainSingle();
        devices.Should().Contain("device2");
    }

    [Fact]
    public void GetUserDevicePermissions_ShouldIsolateUsers()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");
        svc.GrantDeviceAccess("user2", "device2");

        svc.GetUserDevicePermissions("user1").Should().ContainSingle().Which.Should().Be("device1");
        svc.GetUserDevicePermissions("user2").Should().ContainSingle().Which.Should().Be("device2");
    }

    [Fact]
    public void GrantDeviceAccess_MultipleUsers_CanAccessSameDevice()
    {
        var svc = new FineGrainedPermissionService();
        svc.GrantDeviceAccess("user1", "device1");
        svc.GrantDeviceAccess("user2", "device1");

        svc.CanAccessDevice("user1", "device1").Should().BeTrue();
        svc.CanAccessDevice("user2", "device1").Should().BeTrue();
    }

    [Fact]
    public void GrantAndRevoke_ShouldWorkCorrectly_InSequence()
    {
        var svc = new FineGrainedPermissionService();

        // 授予权限
        svc.GrantDeviceAccess("user1", "device1").Should().BeTrue();
        svc.CanAccessDevice("user1", "device1").Should().BeTrue();

        // 再次授予（幂等）
        svc.GrantDeviceAccess("user1", "device1").Should().BeFalse();
        svc.CanAccessDevice("user1", "device1").Should().BeTrue();

        // 撤销权限
        svc.RevokeDeviceAccess("user1", "device1").Should().BeTrue();
        svc.CanAccessDevice("user1", "device1").Should().BeFalse();

        // 再次撤销（不存在）
        svc.RevokeDeviceAccess("user1", "device1").Should().BeFalse();
    }
}
