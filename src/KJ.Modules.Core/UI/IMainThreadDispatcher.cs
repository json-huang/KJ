namespace KJ.Modules.Core.UI;

/// <summary>将工作调度回 WinUI 主线程（WinUI 通常无 SynchronizationContext）。</summary>
public interface IMainThreadDispatcher
{
    bool TryEnqueue(Action action);
}

public static class MainThread
{
    public static IMainThreadDispatcher? Dispatcher { get; set; }

    public static void Enqueue(Action action)
    {
        if (Dispatcher?.TryEnqueue(action) == true)
            return;

        action();
    }
}
