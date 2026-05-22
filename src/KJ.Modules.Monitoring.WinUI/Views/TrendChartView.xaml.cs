using System.Collections.Specialized;
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class TrendChartView : Page
{
    private Polyline? _trendLine;

    public TrendChartView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public TrendChartViewModel? ViewModel => DataContext as TrendChartViewModel;

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.Points.CollectionChanged += OnPointsChanged;
            ViewModel.ChartReady += OnChartReady;
        }
    }

    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DrawChart();
    }

    private void OnChartReady(object? sender, EventArgs e)
    {
        DrawChart();
    }

    private void DrawChart()
    {
        var vm = ViewModel;
        if (vm is null || vm.Points.Count == 0)
        {
            ChartCanvas.Children.Clear();
            return;
        }

        // Ensure the canvas has been measured so ActualWidth/Height are available
        var canvasWidth = ChartCanvas.ActualWidth;
        var canvasHeight = ChartCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Remove old polyline
        if (_trendLine is not null)
        {
            ChartCanvas.Children.Remove(_trendLine);
        }

        var points = vm.Points;
        var minVal = vm.MinValue;
        var maxVal = vm.MaxValue;
        var range = maxVal - minVal;
        if (range < 0.0001) range = 1;

        var padding = 8.0;
        var drawWidth = canvasWidth - padding * 2;
        var drawHeight = canvasHeight - padding * 2;

        var pointCollection = new PointCollection();
        for (var i = 0; i < points.Count; i++)
        {
            var x = padding + (points.Count == 1 ? drawWidth / 2 : drawWidth * i / (points.Count - 1));
            var y = padding + drawHeight - (drawHeight * (points[i].Value - minVal) / range);
            pointCollection.Add(new Windows.Foundation.Point(x, y));
        }

        _trendLine = new Polyline
        {
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.SteelBlue),
            StrokeThickness = 2,
            Points = pointCollection,
        };

        ChartCanvas.Children.Add(_trendLine);
    }
}
