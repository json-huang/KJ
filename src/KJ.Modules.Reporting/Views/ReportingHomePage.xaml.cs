using System.Runtime.InteropServices;
using KJ.Modules.Reporting.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Reporting.Views;

public sealed partial class ReportingHomePage : Page
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    public ReportingHomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ReportingHomeViewModel? ViewModel => DataContext as ReportingHomeViewModel;

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ParentHwnd = GetActiveWindow();
        }
    }
}
