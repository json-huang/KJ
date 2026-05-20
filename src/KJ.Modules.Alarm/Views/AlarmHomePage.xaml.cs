using KJ.Modules.Alarm.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Alarm.Views;

public sealed partial class AlarmHomePage : Page
{
    public AlarmHomePage() => InitializeComponent();
    public AlarmHomeViewModel? ViewModel => DataContext as AlarmHomeViewModel;
}
