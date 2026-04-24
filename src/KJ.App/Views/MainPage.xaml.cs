using KJ.App.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Prism.Navigation.Regions;

namespace KJ.App.Views;

public sealed partial class MainPage : Page
{
    private readonly IRegionManager _regionManager;

    public MainPage(ViewModels.MainPageViewModel viewModel, IRegionManager regionManager)
    {
        InitializeComponent();
        _regionManager = regionManager;
        DataContext = viewModel;
        App.UiDispatcher = DispatcherQueue.GetForCurrentThread();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 默认导航到概览页（HomeOverview 已 RegisterForNavigation）
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("HomeOverview", UriKind.Relative));
    }
}
