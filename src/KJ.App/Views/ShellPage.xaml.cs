using KJ.App.Services;
using KJ.Modules.Auth;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigator _navigator;
    private readonly ISessionResumeService _sessionResume;

    public ShellPage(INavigator navigator, ISessionResumeService sessionResume)
    {
        InitializeComponent();
        _navigator = navigator;
        _sessionResume = sessionResume;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.UiDispatcher ??= DispatcherQueue.GetForCurrentThread();
        await App.WaitForDatabaseInitializationAsync().ConfigureAwait(true);
        _navigator.Attach(RootFrame);
        _ = await _sessionResume.TryResumeAsync().ConfigureAwait(true);
        _navigator.GoMain();
    }
}
