using KJ.App.Services;
using KJ.Modules.Auth;
using KJ.Modules.Core.Regions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace KJ.App.Views;

public sealed partial class MainPage : Page
{
    private readonly IRegionManager _regionManager;
    private readonly ISessionState _sessionState;
    private readonly IContainerProvider _container;

    public MainPage(
        ViewModels.MainPageViewModel viewModel,
        IRegionManager regionManager,
        ISessionState sessionState,
        IContainerProvider container)
    {
        InitializeComponent();
        _regionManager = regionManager;
        _sessionState = sessionState;
        _container = container;
        DataContext = viewModel;
        App.UiDispatcher = DispatcherQueue.GetForCurrentThread();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Populate sidebar navigation by resolving each module's nav view directly.
        MainNavigationHost.Children.Clear();
        MainNavigationHost.Children.Add(_container.Resolve<Navigation.HomeNavigationView>());
        MainNavigationHost.Children.Add(_container.Resolve<KJ.Modules.Auth.Views.AuthNavigationView>());
        MainNavigationHost.Children.Add(_container.Resolve<KJ.Modules.Monitoring.Views.MonitoringNavigationView>());
        MainNavigationHost.Children.Add(_container.Resolve<KJ.Modules.Config.Views.ConfigNavigationView>());
        MainNavigationHost.Children.Add(_container.Resolve<KJ.Modules.Alarm.Views.AlarmNavigationView>());
        MainNavigationHost.Children.Add(_container.Resolve<KJ.Modules.Reporting.Views.ReportingNavigationView>());

        // Defer content navigation so Prism region behaviors finish registering first.
        App.UiDispatcher?.TryEnqueue(() =>
        {
            var target = _sessionState.IsSignedIn ? "HomeOverview" : "AuthLogin";
            _regionManager.RequestNavigate(RegionNames.MainContent, new Uri(target, UriKind.Relative));
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _regionManager.Regions.Remove(RegionNames.MainContent);
    }
}
