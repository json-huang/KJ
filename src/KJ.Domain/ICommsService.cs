namespace KJ.Domain;

/// <summary>
/// 通信服务接口。管理设备采集的启停。
/// </summary>
public interface ICommsService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
