namespace KJ.Infrastructure.Data;

/// <summary>应用数据库初始化完成信号（迁移、种子数据）。</summary>
public interface IDatabaseInitSignal
{
    Task WhenReadyAsync(CancellationToken cancellationToken = default);

    void MarkReady();
}
