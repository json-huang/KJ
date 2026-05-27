using Prism.Navigation;

namespace KJ.Modules.Monitoring.Workflows;

/// <summary>
/// 在区域导航完成前传递 workflowId 等参数（Prism 未必调用 VM.OnNavigatedTo）。
/// </summary>
public static class WorkflowNavigationBridge
{
    private static INavigationParameters? _pending;

    public static void SetPending(INavigationParameters parameters) =>
        _pending = parameters;

    public static INavigationParameters? TakePending()
    {
        var p = _pending;
        _pending = null;
        return p;
    }
}
