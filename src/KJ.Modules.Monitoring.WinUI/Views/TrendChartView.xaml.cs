using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class TrendChartView : Page
{
    public TrendChartView() => InitializeComponent();

    public TrendChartViewModel? ViewModel => DataContext as TrendChartViewModel;
}
