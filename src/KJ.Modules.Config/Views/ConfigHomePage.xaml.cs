using KJ.Modules.Config.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Config.Views;

public sealed partial class ConfigHomePage : Page
{
    public ConfigHomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ConfigHomeViewModel? ViewModel => DataContext as ConfigHomeViewModel;

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is not null)
            ViewModel?.AttachDispatcher(dispatcher);

        ViewModel?.Refresh();
    }
}
