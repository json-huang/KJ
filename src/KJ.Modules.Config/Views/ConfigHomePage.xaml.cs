using KJ.Modules.Config.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Config.Views;

public sealed partial class ConfigHomePage : Page
{
    public ConfigHomePage() => InitializeComponent();
    public ConfigHomeViewModel? ViewModel => DataContext as ConfigHomeViewModel;
}
