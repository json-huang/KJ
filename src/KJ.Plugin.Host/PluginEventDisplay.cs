using System.Text.Json;
using KJ.Plugin.Contracts;

namespace KJ.Plugin.Host;

public static class PluginEventDisplay
{
    public static bool ShouldNotify(PluginEvent pluginEvent) =>
        !string.Equals(pluginEvent.Topic, PluginProtocol.Topics.Heartbeat, StringComparison.Ordinal) &&
        !string.Equals(pluginEvent.Topic, PluginProtocol.Topics.HostEventReceived, StringComparison.Ordinal);

    public static (string Title, string Message) Format(PluginEvent pluginEvent)
    {
        var localTime = DateTimeOffset.FromUnixTimeMilliseconds(pluginEvent.UnixTimeMs).ToLocalTime();
        var summary = TryFormatPayload(pluginEvent);
        var message =
            $"插件：{pluginEvent.PluginId}{Environment.NewLine}" +
            $"主题：{pluginEvent.Topic}{Environment.NewLine}" +
            $"时间：{localTime:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
            summary;

        return ("收到插件信息", message);
    }

    private static string TryFormatPayload(PluginEvent pluginEvent)
    {
        if (string.IsNullOrWhiteSpace(pluginEvent.PayloadJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(pluginEvent.PayloadJson);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return $"内容：{message.GetString()}{Environment.NewLine}原始：{pluginEvent.PayloadJson}";

            return $"原始：{pluginEvent.PayloadJson}";
        }
        catch
        {
            return $"原始：{pluginEvent.PayloadJson}";
        }
    }
}
