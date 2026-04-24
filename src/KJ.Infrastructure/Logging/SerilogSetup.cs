using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KJ.Infrastructure.Logging;

public static class SerilogSetup
{
    public static IServiceCollection AddKjSerilog(this IServiceCollection services, string logFilePath = "logs/kj-.log")
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
            .CreateLogger();

        services.AddLogging(lb => lb.AddSerilog(dispose: true));
        return services;
    }
}

