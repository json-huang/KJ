namespace KJ.Infrastructure.Data;

public sealed class DatabaseInitSignal : IDatabaseInitSignal
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WhenReadyAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
            return _ready.Task.WaitAsync(cancellationToken);

        return _ready.Task;
    }

    public void MarkReady() => _ready.TrySetResult();
}
