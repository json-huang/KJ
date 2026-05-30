namespace KJ.Plugin.Host;

public enum PluginConnectionState
{
    Disconnected,
    Starting,
    Connecting,
    Reconnecting,
    Connected,
    Faulted,
}
