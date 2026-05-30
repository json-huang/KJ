using KJ.Plugin.Contracts;
using KJ.Plugin.Sample.WinForms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    private const string DefaultEndpoint = "http://127.0.0.1:50551";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var endpoint = PluginLaunchConfiguration.ResolveEndpoint(args, DefaultEndpoint);
        if (!PluginLaunchConfiguration.TryGetListenPort(endpoint, out var port))
            port = 50551;

        using var form = new SamplePluginForm();
        SamplePluginService.BindForm(form);
        form.Show();

        using var app = CreateGrpcApp(port);
        var serverTask = app.RunAsync();

        Application.Run(form);

        SamplePluginService.EnqueueEvent(new PluginEvent
        {
            PluginId = SamplePluginService.PluginId,
            Topic = PluginProtocol.Topics.Heartbeat,
            PayloadJson = "{\"status\":\"shutdown\"}",
            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        app.StopAsync(shutdownCts.Token).GetAwaiter().GetResult();
        serverTask.GetAwaiter().GetResult();
    }

    private static WebApplication CreateGrpcApp(int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
        builder.Services.AddGrpc();

        var app = builder.Build();
        app.MapGrpcService<SamplePluginService>();
        app.MapGet("/", () => "KJ sample plugin is running.");
        return app;
    }
}
