using KJ.Modules.Core.UI;
using Microsoft.UI.Dispatching;

namespace KJ.App.Services;

public sealed class WinUiMainThreadDispatcher : IMainThreadDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiMainThreadDispatcher(DispatcherQueue queue) => _queue = queue;

    public bool TryEnqueue(Action action) => _queue.TryEnqueue(() => action());
}
