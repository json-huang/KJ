using System.Threading.Tasks;
using System.IO;
using KJ.App.Dialogs;
using KJ.App.Services;
using KJ.App.ViewModels.Dialogs;
using KJ.App.Views;
using KJ.App.Views.Navigation;
using KJ.Infrastructure.DependencyInjection;
using KJ.Modules.Auth;
using Prism.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Prism.Ioc;
using Prism.Modularity;
using KJ.Workflows;
using Microsoft.EntityFrameworkCore;

namespace KJ.App;

public partial class App : PrismApplication
{
    private static readonly TaskCompletionSource<bool> DatabaseInitializationCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static DispatcherQueue? UiDispatcher { get; set; }
    public static Window? MainWindow { get; private set; }

    public static Task WaitForDatabaseInitializationAsync() => DatabaseInitializationCompleted.Task;

    public App()
    {
        InitializeComponent();
        HookGlobalExceptionLogging();
    }

    protected override void ConfigureWindow(Window window)
    {
        base.ConfigureWindow(window);
        MainWindow = window;
        window.Title = "KJ";
        window.Activate();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
        services.AddKjPersistence(configuration);
        services.AddKjMessaging();

        // Shared domain singletons must live in the MS.DI container so that
        // MassTransit consumers and Prism viewmodels observe the same instance.
        // Inner in-memory services are wrapped with EF Core persistence.
        services.AddSingleton<KJ.Domain.ITagStore, KJ.Domain.Services.InMemoryTagStore>();
        services.AddSingleton<KJ.Domain.Services.AlarmService>();
        services.AddSingleton<KJ.Domain.Services.RecipeEngine>();
        services.AddSingleton<KJ.Domain.Services.DeviceManager>();
        services.AddSingleton<KJ.Domain.IAlarmService>(sp =>
            new KJ.Infrastructure.Services.EfAlarmService(
                sp.GetRequiredService<KJ.Domain.Services.AlarmService>(),
                sp.GetRequiredService<IDbContextFactory<KJ.Infrastructure.Data.KjDbContext>>()));
        services.AddSingleton<KJ.Domain.IRecipeEngine>(sp =>
            new KJ.Infrastructure.Services.EfRecipeEngine(
                sp.GetRequiredService<KJ.Domain.Services.RecipeEngine>(),
                sp.GetRequiredService<IDbContextFactory<KJ.Infrastructure.Data.KjDbContext>>()));
        services.AddSingleton<KJ.Domain.IDeviceManager>(sp =>
            new KJ.Infrastructure.Services.EfDeviceManager(
                sp.GetRequiredService<KJ.Domain.Services.DeviceManager>(),
                sp.GetRequiredService<IDbContextFactory<KJ.Infrastructure.Data.KjDbContext>>()));

        // 设备驱动
        services.AddSingleton<KJ.Drivers.TcpDeviceDriver>();
        services.AddSingleton<KJ.Drivers.ModbusTcpDriver>();
        services.AddSingleton<KJ.Drivers.ModbusRtuDriver>();
        services.AddSingleton<KJ.Drivers.OpcUaDriver>();
        services.AddSingleton<KJ.Drivers.Plc.Beckhoff.Ads.BeckhoffAdsDriver>();
        services.AddSingleton<KJ.Drivers.Abstractions.IDeviceDriver>(sp =>
            sp.GetRequiredService<KJ.Drivers.Plc.Beckhoff.Ads.BeckhoffAdsDriver>());
        services.AddSingleton<KJ.Drivers.Abstractions.IDeviceDriverFactory, KJ.Drivers.DeviceDriverFactory>();

        // 标签配置 & 设备采集
        services.AddSingleton<KJ.Infrastructure.Services.EfTagConfigStore>();
        services.AddSingleton<KJ.Domain.ITagConfigStore>(sp =>
            sp.GetRequiredService<KJ.Infrastructure.Services.EfTagConfigStore>());
        services.AddSingleton<KJ.Comms.Abstractions.ICommsService, KJ.Core.DevicePollingService>();
        services.AddSingleton<KJ.Core.DevicePollingService>(sp =>
            (KJ.Core.DevicePollingService)sp.GetRequiredService<KJ.Comms.Abstractions.ICommsService>());

        // TagHistory 持久化（监听 TagUpdated 事件，异步写入 DB）
        services.AddSingleton<KJ.Infrastructure.Services.TagHistoryWriter>();

        // 数据保留策略（每天清理超过 30 天的历史数据）
        services.AddSingleton<KJ.Infrastructure.Services.TagHistoryRetentionService>();

        // 健康检查
        services.AddSingleton<KJ.Infrastructure.Services.HealthCheckService>();

        // OpenTelemetry 指标
        services.AddSingleton<KJ.Infrastructure.Metrics.KjMetrics>();
        services.AddSingleton<KJ.Infrastructure.Metrics.MetricsCollector>();

        // 多语言
        services.AddSingleton<KJ.Infrastructure.Localization.LocalizationService>();

        // 告警历史查询
        services.AddSingleton<KJ.Infrastructure.Services.AlarmHistoryService>();

        // 工作流导入导出
        services.AddSingleton<KJ.Infrastructure.Services.WorkflowImportExportService>();

        // 标签搜索
        services.AddSingleton<KJ.Domain.Services.TagSearchService>();

        // 扩展认证（密码重置、注册）
        services.AddSingleton<KJ.Infrastructure.Auth.ExtendedAuthService>();

        // 工作流模板
        services.AddSingleton<KJ.Infrastructure.Services.WorkflowTemplateService>();

        // 设备详情
        services.AddSingleton<KJ.Infrastructure.Services.DeviceDetailsService>();

        // 操作审计查询
        services.AddSingleton<KJ.Infrastructure.Services.AuditQueryService>();

        // 数据保留策略管理
        services.AddSingleton<KJ.Infrastructure.Services.DataRetentionManager>();

        // 数据库备份
        services.AddSingleton<KJ.Infrastructure.Services.DatabaseBackupService>();

        // 配置导入导出
        services.AddSingleton<KJ.Infrastructure.Services.ConfigImportExportService>();

        // 会话管理
        services.AddSingleton<KJ.Infrastructure.Auth.SessionManager>();

        // 细粒度权限
        services.AddSingleton<KJ.Infrastructure.Auth.FineGrainedPermissionService>();

        // 配置变更审计
        services.AddSingleton<KJ.Infrastructure.Services.ConfigChangeAuditService>();

        // 工作流撤销重做
        services.AddSingleton<KJ.Workflows.WorkflowUndoRedoService>();

        // 工作流剪贴板
        services.AddSingleton<KJ.Workflows.WorkflowClipboardService>();

        // 定时报表
        services.AddSingleton<KJ.Infrastructure.Services.ScheduledReportService>();

        // 报表导出
        services.AddSingleton<KJ.Infrastructure.Services.TagHistoryExportService>();

        // 告警通知
        services.AddSingleton<KJ.Domain.Services.TagManager>();
        services.AddSingleton<KJ.Domain.Services.AlarmNotificationService>();

        // Workflow runtime
        services.AddSingleton<KJ.Workflows.PlcDataBridge>();
        services.AddSingleton<KJ.Workflows.ScriptCompilationService>();
        services.AddSingleton<IWorkflowRunLogStore, KJ.Infrastructure.Workflows.EfWorkflowRunLogStore>();
        services.AddSingleton<IWorkflowStepHandler, KJ.Infrastructure.Workflows.StartStepHandler>();
        services.AddSingleton<IWorkflowStepHandler, KJ.Infrastructure.Workflows.ReadTagStepHandler>();
        services.AddSingleton<IWorkflowStepHandler, KJ.Infrastructure.Workflows.WriteTagStepHandler>();
        services.AddSingleton<IWorkflowRuntime, WorkflowRuntimeService>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // TODO: 后续在 Infra/Domain/Modules 落地后，统一在这里注册
        containerRegistry.RegisterSingleton<ISessionState, SessionState>();
        containerRegistry.RegisterSingleton<INavigator, FrameNavigator>();
        containerRegistry.RegisterSingleton<IAuthenticationContext, AuthenticationContext>();
        containerRegistry.RegisterSingleton<ILoginCredentialStore, LoginCredentialStore>();
        containerRegistry.RegisterSingleton<ISessionResumeService, SessionResumeService>();
        containerRegistry.RegisterSingleton<IShellContentNavigation, ShellRegionNavigationAdapter>();
        containerRegistry.RegisterSingleton<IPermissionService, PermissionService>();

        containerRegistry.RegisterDialogWindow<DialogWindow>();
        containerRegistry.RegisterDialog<AboutDialog, AboutDialogViewModel>("About");

        containerRegistry.RegisterForNavigation<HomeOverviewPage, ViewModels.HomeOverviewViewModel>("HomeOverview");
        containerRegistry.Register<Views.Navigation.HomeNavigationView>();

        containerRegistry.Register<Views.ShellPage>();
        containerRegistry.Register<Views.LoginPage>(); // legacy：已改为 AuthLogin 走 MainContent 区域
        containerRegistry.RegisterForNavigation<Views.MainPage>();
        containerRegistry.Register<ViewModels.MainPageViewModel>();
        containerRegistry.Register<ViewModels.LoginViewModel>(); // legacy：对应 LoginPage

        // Domain services are registered in ConfigureServices so both MassTransit and Prism share instances.
    }

    protected override UIElement CreateShell()
    {
        return Container.Resolve<Views.ShellPage>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<KJ.Modules.Auth.AuthModule>();
        moduleCatalog.AddModule<KJ.Modules.Monitoring.MonitoringModule>();
        moduleCatalog.AddModule<KJ.Modules.Config.ConfigModule>();
        moduleCatalog.AddModule<KJ.Modules.Alarm.AlarmModule>();
        moduleCatalog.AddModule<KJ.Modules.Reporting.ReportingModule>();
    }

    protected override async void OnInitialized()
    {
        base.OnInitialized();
        try
        {
            // Ensure messaging bridges are instantiated early.
            _ = Container.Resolve<KJ.Infrastructure.Messaging.TagValuePublishingBridge>();

            await InitializeDatabaseAsync().ConfigureAwait(false);
        }
        finally
        {
            DatabaseInitializationCompleted.TrySetResult(true);
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            var scopeFactory = Container.Resolve<IServiceScopeFactory>();
            await using var scope = scopeFactory.CreateAsyncScope();
            var initializer = scope.ServiceProvider.GetRequiredService<KJ.Infrastructure.Data.DatabaseInitializer>();
            await initializer.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database init failed: {ex}");
        }
    }

    private void HookGlobalExceptionLogging()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "KJ.App-crash.log");

        static string Format(object? ex) => ex?.ToString() ?? "<null>";

        void Write(string message)
        {
            try
            {
                File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // best-effort only
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write($"AppDomain.UnhandledException: {Format(e.ExceptionObject)}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write($"TaskScheduler.UnobservedTaskException: {Format(e.Exception)}");
            e.SetObserved();
        };

        UnhandledException += (_, e) =>
        {
            Write($"Application.UnhandledException: {Format(e.Exception)}");
            // keep default behavior in release; we only need evidence
        };
    }
}
