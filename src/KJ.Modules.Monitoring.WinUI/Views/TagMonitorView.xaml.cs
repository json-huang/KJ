using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class TagMonitorView : Page
{
    public TagMonitorView() => InitializeComponent();

    public TagMonitorViewModel? ViewModel => DataContext as TagMonitorViewModel;
}
