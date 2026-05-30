using KJ.Drivers.Abstractions;
using KJ.Workflows;

namespace KJ.Core;

public sealed class WorkflowPlcConnection : IWorkflowPlcConnection
{
    private readonly DevicePollingService _polling;

    public WorkflowPlcConnection(DevicePollingService polling) => _polling = polling;

    public Task ConnectDeviceAsync(string deviceId, CancellationToken ct = default) =>
        _polling.ConnectNowAsync(deviceId, ct);

    public bool TryGetConnectedDriver(string deviceId, out IDeviceDriver? driver) =>
        _polling.TryGetConnectedDriver(deviceId, out driver);
}
