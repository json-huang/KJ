using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace KJ.Infrastructure.Logging;

public static class SerilogSetup
{
    /// <summary>
    /// 配置结构化日志。支持：
    /// - 控制台输出（开发环境友好）
    /// - 文件输出（JSON 格式，按天滚动）
    /// - 日志级别从 appsettings 配置
    /// - 自动 enrich：机器名、进程 ID、线程 ID
    /// </summary>
    public static IServiceCollection AddKjSerilog(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string logFilePath = "logs/kj-.log")
    {
        var loggerConfig = new LoggerConfiguration()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "KJ");

        // 从 appsettings 读取日志级别
        var logLevel = configuration?["Logging:LogLevel:Default"] ?? "Information";
        if (Enum.TryParse<LogEventLevel>(logLevel, true, out var level))
            loggerConfig.MinimumLevel.Is(level);
        else
            loggerConfig.MinimumLevel.Information();

        // 系统命名空间降低噪音
        loggerConfig.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
        loggerConfig.MinimumLevel.Override("System", LogEventLevel.Warning);
        loggerConfig.MinimumLevel.Override("MassTransit", LogEventLevel.Warning);

        // 控制台输出（人类可读）
        loggerConfig.WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        // 文件输出（JSON 格式，便于日志聚合系统解析）
        loggerConfig.WriteTo.File(
            new CompactJsonFormatter(),
            logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 100 * 1024 * 1024, // 100MB
            rollOnFileSizeLimit: true);

        // 诊断日志单独文件（驱动层 DiagnosticEvent）
        loggerConfig.WriteTo.File(
            new CompactJsonFormatter(),
            "logs/kj-diagnostics-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            restrictedToMinimumLevel: LogEventLevel.Debug);

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(lb => lb.AddSerilog(dispose: true));
        return services;
    }

    /// <summary>优雅关闭日志。</summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
