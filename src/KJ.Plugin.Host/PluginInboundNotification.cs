using KJ.Plugin.Contracts;

namespace KJ.Plugin.Host;

/// <summary>插件 → 主机的入站事件，供 UI 层订阅（不依赖具体页面生命周期）。</summary>
public static class PluginInboundNotification
{
    public static event Action<PluginEvent>? Received;

    internal static void Publish(PluginEvent pluginEvent) => Received?.Invoke(pluginEvent);
}
