using KJ.Modules.Reporting.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Reporting.Views;

public sealed partial class ReportingHomePage : Page
{
    public ReportingHomePage() => InitializeComponent();
    public ReportingHomeViewModel? ViewModel => DataContext as ReportingHomeViewModel;
}
