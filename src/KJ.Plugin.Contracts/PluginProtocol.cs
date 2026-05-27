namespace KJ.Plugin.Contracts;

public static class PluginProtocol
{
    public const int CurrentVersion = 1;
    public const string HostId = "KJ";

    public static class Capabilities
    {
        public const string WindowHandle = "window-handle";
        public const string Commands = "commands";
        public const string Events = "events";
        public const string HostEvents = "host-events";
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
