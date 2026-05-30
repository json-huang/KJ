namespace KJ.Plugin.Contracts;

public static class PluginProtocol
{
    public const int MinSupportedVersion = 1;
    public const int CurrentVersion = 1;
    public const string HostId = "KJ";

    public static class Capabilities
    {
        public const string WindowHandle = "window-handle";
        public const string Commands = "commands";
        public const string Events = "events";
        public const string HostEvents = "host-events";
    }

    /// <summary>宿主启动插件进程时注入的环境变量/命令行参数。</summary>
    public static class Launch
    {
        public const string EndpointEnv = "KJ_PLUGIN_ENDPOINT";
        public const string HostIdEnv = "KJ_PLUGIN_HOST_ID";
        public const string ProtocolVersionEnv = "KJ_PLUGIN_PROTOCOL_VERSION";
        public const string EndpointArgPrefix = "--kj-endpoint=";
        public const string HostIdArgPrefix = "--kj-host-id=";
    }

    public static class Topics
    {
        public const string Heartbeat = "plugin.heartbeat";
        public const string TestInfo = "plugin.test-info";
        public const string HostEventReceived = "plugin.host-event-received";
        public const string TagValueChanged = "host.tag-value-changed";
        public const string AlarmRaised = "host.alarm-raised";
        public const string WorkflowRunChanged = "host.workflow-run-changed";
        public const string UserSessionChanged = "host.user-session-changed";
        public const string HostShutdown = "host.shutdown";
    }
}
