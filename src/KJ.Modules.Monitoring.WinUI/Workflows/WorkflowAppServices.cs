using KJ.Modules.Core.Diagnostics;
using KJ.Plugin.Host;
using KJ.Workflows;
using Microsoft.UI.Xaml;

namespace KJ.Modules.Monitoring.Workflows;

/// <summary>
/// 工作流运行时由 App 在 MS.DI 中注册；编辑页打开时延迟取用，避免 DryIoc 区域作用域死锁。
/// </summary>
public static class WorkflowAppServices
{
    public static Func<IWorkflowRuntime>? GetRuntime { get; set; }

    public static Action? ActivateMainWindow { get; set; }

    public static Func<IntPtr>? GetMainWindowHandle { get; set; }

    public static Func<PluginManager>? GetPluginManager { get; set; }

    public static Func<XamlRoot?>? GetDialogXamlRoot { get; set; }

    public static IntPtr ResolveMainWindowHandle() => GetMainWindowHandle?.Invoke() ?? IntPtr.Zero;

    public static XamlRoot? ResolveDialogXamlRoot() => GetDialogXamlRoot?.Invoke();

    public static PluginManager? ResolvePluginManager() => GetPluginManager?.Invoke();

    public static IWorkflowRuntime ResolveRuntime()
    {
        if (GetRuntime is null)
            throw new InvalidOperationException("工作流运行时未初始化，请重启应用。");

        try
        {
            var runtime = GetRuntime.Invoke();
            NavTrace.Write($"WorkflowAppServices.ResolveRuntime: ok state={runtime.State}");
            return runtime;
        }
        catch (Exception ex)
        {
            NavTrace.Write($"WorkflowAppServices.ResolveRuntime: FAILED {ex}");
            throw;
        }
    }
}
