using KJ.Drivers.Abstractions;

namespace KJ.Workflows;

/// <summary>
/// 工作流读写 PLC 时复用设备轮询/手动连接已建立的 ADS 会话。
/// </summary>
public interface IWorkflowPlcConnection
{
    Task ConnectDeviceAsync(string deviceId, CancellationToken ct = default);

    bool TryGetConnectedDriver(string deviceId, out IDeviceDriver? driver);
}
