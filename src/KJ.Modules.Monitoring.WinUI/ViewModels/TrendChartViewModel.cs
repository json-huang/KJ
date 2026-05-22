using System.Collections.ObjectModel;
using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class TrendChartViewModel : BindableBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<TrendPoint> Points { get; } = new();

    public event EventHandler? ChartReady;

    private string _selectedTagKey = string.Empty;
    public string SelectedTagKey
    {
        get => _selectedTagKey;
        set => SetProperty(ref _selectedTagKey, value);
    }

    private string _statusText = "输入标签 Key 后点击查询";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private double _minValue;
    public double MinValue
    {
        get => _minValue;
        private set => SetProperty(ref _minValue, value);
    }

    private double _maxValue;
    public double MaxValue
    {
        get => _maxValue;
        private set => SetProperty(ref _maxValue, value);
    }

    private int _pointCount;
    public int PointCount
    {
        get => _pointCount;
        private set => SetProperty(ref _pointCount, value);
    }

    public DelegateCommand LoadCommand { get; }

    public TrendChartViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        LoadCommand = new DelegateCommand(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTagKey)) return;

        StatusText = "加载中...";
        Points.Clear();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KjDbContext>();
            var tagId = TagIdentity.GetTagId(SelectedTagKey);
            var history = await db.TagHistory
                .Where(h => h.TagId == tagId)
                .OrderByDescending(h => h.Timestamp)
                .Take(200)
                .ToListAsync();

            foreach (var h in history.AsEnumerable().Reverse())
            {
                Points.Add(new TrendPoint
                {
                    Timestamp = h.Timestamp.ToString("HH:mm:ss"),
                    Value = double.TryParse(h.Value, out var v) ? v : 0,
                });
            }

            UpdateChartMetrics();
            StatusText = $"已加载 {Points.Count} 个数据点";
            ChartReady?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    private void UpdateChartMetrics()
    {
        PointCount = Points.Count;
        if (PointCount == 0)
        {
            MinValue = 0;
            MaxValue = 0;
            return;
        }

        MinValue = Points.Min(p => p.Value);
        MaxValue = Points.Max(p => p.Value);

        // Ensure a visible range even when all values are equal
        if (Math.Abs(MaxValue - MinValue) < 0.0001)
        {
            MinValue -= 1;
            MaxValue += 1;
        }
    }
}

public sealed class TrendPoint
{
    public string Timestamp { get; set; } = string.Empty;
    public double Value { get; set; }
}
