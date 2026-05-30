namespace KJ.Plugin.Contracts;

/// <summary>解析宿主注入的 gRPC 端点（环境变量优先，其次命令行）。</summary>
public static class PluginLaunchConfiguration
{
    public static string ResolveEndpoint(string[] args, string defaultEndpoint)
    {
        var fromEnv = Environment.GetEnvironmentVariable(PluginProtocol.Launch.EndpointEnv);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        foreach (var arg in args)
        {
            if (arg.StartsWith(PluginProtocol.Launch.EndpointArgPrefix, StringComparison.OrdinalIgnoreCase))
                return arg[PluginProtocol.Launch.EndpointArgPrefix.Length..].Trim();
        }

        return defaultEndpoint;
    }

    public static bool TryGetListenPort(string endpoint, out int port)
    {
        port = 0;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;

        port = uri.Port;
        return port > 0;
    }
}
