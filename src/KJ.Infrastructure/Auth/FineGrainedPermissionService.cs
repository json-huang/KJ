using System.Collections.Concurrent;

namespace KJ.Infrastructure.Auth;

/// <summary>
/// 细粒度权限服务。支持按设备级别控制用户访问权限。
/// </summary>
public sealed class FineGrainedPermissionService
{
    // userId -> HashSet of accessible deviceIds
    private readonly ConcurrentDictionary<string, HashSet<string>> _userDevicePermissions = new();

    /// <summary>获取用户可访问的设备列表。</summary>
    public IReadOnlyList<string> GetUserDevicePermissions(string userId)
    {
        if (_userDevicePermissions.TryGetValue(userId, out var devices))
        {
            lock (devices)
            {
                return devices.ToList().AsReadOnly();
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>检查用户是否能访问指定设备。</summary>
    public bool CanAccessDevice(string userId, string deviceId)
    {
        if (_userDevicePermissions.TryGetValue(userId, out var devices))
        {
            lock (devices)
            {
                return devices.Contains(deviceId);
            }
        }

        return false;
    }

    /// <summary>授予用户设备访问权限。返回是否为新增（false 表示已有权限）。</summary>
    public bool GrantDeviceAccess(string userId, string deviceId)
    {
        var devices = _userDevicePermissions.GetOrAdd(userId, _ => new HashSet<string>());
        lock (devices)
        {
            return devices.Add(deviceId);
        }
    }

    /// <summary>撤销用户设备访问权限。返回是否成功移除（false 表示原本无权限）。</summary>
    public bool RevokeDeviceAccess(string userId, string deviceId)
    {
        if (_userDevicePermissions.TryGetValue(userId, out var devices))
        {
            lock (devices)
            {
                return devices.Remove(deviceId);
            }
        }

        return false;
    }

    /// <summary>获取可访问指定设备的所有用户列表。</summary>
    public IReadOnlyList<string> GetUsersWithDeviceAccess(string deviceId)
    {
        var result = new List<string>();
        foreach (var kvp in _userDevicePermissions)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.Contains(deviceId))
                    result.Add(kvp.Key);
            }
        }
        return result.AsReadOnly();
    }

    /// <summary>撤销用户的所有设备访问权限。</summary>
    public int RevokeAllDeviceAccess(string userId)
    {
        if (_userDevicePermissions.TryRemove(userId, out var devices))
        {
            lock (devices)
            {
                return devices.Count;
            }
        }

        return 0;
    }
}
