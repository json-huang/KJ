using System.Diagnostics;

namespace KJ.Plugin.Host;

/// <summary>宿主向插件进程传递的启动参数（环境变量 + 命令行）。</summary>
public static class PluginLaunchOptions
{
    public static void ApplyTo(ProcessStartInfo startInfo, PluginDescriptor descriptor)
    {
        startInfo.Environment[Contracts.PluginProtocol.Launch.EndpointEnv] = descriptor.GrpcEndpoint;
        startInfo.Environment[Contracts.PluginProtocol.Launch.HostIdEnv] = Contracts.PluginProtocol.HostId;
        startInfo.Environment[Contracts.PluginProtocol.Launch.ProtocolVersionEnv] =
            Contracts.PluginProtocol.CurrentVersion.ToString();

        var endpointArg = $"{Contracts.PluginProtocol.Launch.EndpointArgPrefix}{descriptor.GrpcEndpoint}";
        var hostArg = $"{Contracts.PluginProtocol.Launch.HostIdArgPrefix}{Contracts.PluginProtocol.HostId}";
        startInfo.ArgumentList.Add(endpointArg);
        startInfo.ArgumentList.Add(hostArg);
    }
}
