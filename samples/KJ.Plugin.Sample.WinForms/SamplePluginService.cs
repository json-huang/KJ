using System.Text.Json;
using System.Threading.Channels;
using Grpc.Core;
using KJ.Plugin.Contracts;

namespace KJ.Plugin.Sample.WinForms;

public sealed class SamplePluginService : PluginService.PluginServiceBase
{
    public const string PluginId = "kj.sample.winforms";
    private static readonly Channel<PluginEvent> Events = Channel.CreateUnbounded<PluginEvent>();

    public static Func<IntPtr>? WindowHandleProvider { get; set; }

    private static SamplePluginForm? _form;

    public const string TestInfoTopic = PluginProtocol.Topics.TestInfo;

    public static void BindForm(SamplePluginForm form)
    {
        _form = form;
        WindowHandleProvider = () =>
            _form is { IsHandleCreated: true, Visible: true } ? _form.Handle : IntPtr.Zero;
    }

    public static void EnqueueHeartbeat(string source)
    {
        EnqueueEvent(new PluginEvent
        {
            PluginId = PluginId,
            Topic = PluginProtocol.Topics.Heartbeat,
            PayloadJson = $"{{\"source\":\"{source}\",\"time\":\"{DateTimeOffset.Now:O}\"}}",
            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    public static void EnqueueTestInfo(string? message = null)
    {
        var text = message ?? "来自示例 WinForms 插件的测试信息";
        EnqueueEvent(new PluginEvent
        {
            PluginId = PluginId,
            Topic = TestInfoTopic,
            PayloadJson = JsonSerializer.Serialize(new { message = text, time = DateTimeOffset.Now }),
            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    public static void EnqueueEvent(PluginEvent pluginEvent)
    {
        Events.Writer.TryWrite(pluginEvent);
    }

    public override Task<HandshakeReply> Handshake(HandshakeRequest request, ServerCallContext context)
    {
        var accepted = request.ProtocolVersion == PluginProtocol.CurrentVersion;
        return Task.FromResult(new HandshakeReply
        {
            PluginId = PluginId,
            PluginVersion = "1.0.0",
            ProtocolVersion = PluginProtocol.CurrentVersion,
            Accepted = accepted,
            Message = accepted ? "ok" : "protocol version mismatch",
            Capabilities =
            {
                PluginProtocol.Capabilities.WindowHandle,
                PluginProtocol.Capabilities.Commands,
                PluginProtocol.Capabilities.Events,
                PluginProtocol.Capabilities.HostEvents,
            },
        });
    }

    public override Task<PluginManifest> GetManifest(GetManifestRequest request, ServerCallContext context)
    {
        var manifest = new PluginManifest
        {
            PluginId = PluginId,
            DisplayName = "示例 WinForms 插件",
            Description = "演示外部进程通过 gRPC 提供窗口、命令和事件。",
            Version = "1.0.0",
            Pages =
            {
                new PluginPage { PageId = "main", Title = "示例插件窗口", DockPosition = "right" },
            },
            Commands =
            {
                new PluginCommand { CommandId = "sample.ping", Title = "Ping", Description = "返回插件在线状态" },
            },
            Subscriptions =
            {
                PluginProtocol.Topics.TagValueChanged,
                PluginProtocol.Topics.AlarmRaised,
                PluginProtocol.Topics.WorkflowRunChanged,
            },
        };

        return Task.FromResult(manifest);
    }

    public override Task<WindowHandleReply> GetWindow(GetWindowRequest request, ServerCallContext context)
    {
        var hwnd = WindowHandleProvider?.Invoke() ?? IntPtr.Zero;
        return Task.FromResult(new WindowHandleReply
        {
            Hwnd = hwnd.ToInt64(),
            Title = "示例插件窗口",
            Available = hwnd != IntPtr.Zero,
            Message = hwnd == IntPtr.Zero ? "window is not ready" : "ok",
        });
    }

    public override Task<CommandReply> InvokeCommand(CommandRequest request, ServerCallContext context)
    {
        if (request.CommandId == "prepare.embed")
        {
            if (_form is null)
            {
                return Task.FromResult(new CommandReply
                {
                    Success = false,
                    Message = "plugin form is not ready",
                });
            }

            _form.Invoke(() => _form.PrepareForEmbed());
            return Task.FromResult(new CommandReply
            {
                Success = true,
                Message = "ready for embed",
            });
        }

        if (request.CommandId == "sample.ping")
        {
            EnqueueHeartbeat("command");
            return Task.FromResult(new CommandReply
            {
                Success = true,
                Message = "pong",
                Values = { { "time", DateTimeOffset.Now.ToString("O") } },
            });
        }

        return Task.FromResult(new CommandReply
        {
            Success = false,
            Message = $"Unknown command: {request.CommandId}",
        });
    }

    public override async Task SubscribeEvents(EventSubscribeRequest request, IServerStreamWriter<PluginEvent> responseStream, ServerCallContext context)
    {
        EnqueueHeartbeat("subscribe");
        await foreach (var pluginEvent in Events.Reader.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
            await responseStream.WriteAsync(pluginEvent).ConfigureAwait(false);
    }

    public override Task<Ack> PushHostEvent(HostEvent request, ServerCallContext context)
    {
        EnqueueEvent(new PluginEvent
        {
            PluginId = PluginId,
            Topic = PluginProtocol.Topics.HostEventReceived,
            PayloadJson = $"{{\"topic\":\"{request.Topic}\",\"payload\":{request.PayloadJson}}}",
            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        return Task.FromResult(new Ack { Success = true, Message = "received" });
    }
}
