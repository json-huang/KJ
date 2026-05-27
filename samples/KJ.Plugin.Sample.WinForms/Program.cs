using KJ.Plugin.Contracts;
using KJ.Plugin.Sample.WinForms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    private const string Endpoint = "http://127.0.0.1:50551";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var form = new SamplePluginForm();
        SamplePluginService.BindForm(form);
        form.Show();

        using var app = CreateGrpcApp(args);
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

    private static WebApplication CreateGrpcApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(50551, listenOptions =>
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
