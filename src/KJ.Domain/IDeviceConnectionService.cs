namespace KJ.Domain;

/// <summary>
/// 设备手动连接/断开接口（用于 UI 上的“连接/断开”按钮）。
/// </summary>
public interface IDeviceConnectionService
{
    Task ConnectAsync(string deviceId, CancellationToken ct = default);
    Task DisconnectAsync(string deviceId, CancellationToken ct = default);
}

