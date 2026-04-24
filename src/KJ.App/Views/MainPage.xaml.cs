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
        // 先保持默认入口稳定；后续再切回“监控 → 设备列表”作为启动页
        _regionManager.RequestNavigate(KJ.Modules.Core.Regions.RegionNames.MainContent, new Uri("HomeOverview", UriKind.Relative));
    }
}
