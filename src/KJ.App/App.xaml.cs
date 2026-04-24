using System.Threading.Tasks;
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

namespace KJ.App;

public partial class App : PrismApplication
{
    private static readonly TaskCompletionSource<bool> DatabaseInitializationCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static DispatcherQueue? UiDispatcher { get; set; }

    public static Task WaitForDatabaseInitializationAsync() => DatabaseInitializationCompleted.Task;

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

        containerRegistry.RegisterSingleton<KJ.Domain.ITagStore, KJ.Domain.Services.InMemoryTagStore>();
        containerRegistry.RegisterSingleton<KJ.Domain.IAlarmService, KJ.Domain.Services.AlarmService>();
        containerRegistry.RegisterSingleton<KJ.Domain.IRecipeEngine, KJ.Domain.Services.RecipeEngine>();
        containerRegistry.RegisterSingleton<KJ.Domain.IDeviceManager, KJ.Domain.Services.DeviceManager>();
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
}
