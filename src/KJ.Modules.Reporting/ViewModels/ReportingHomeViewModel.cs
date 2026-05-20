using System.Collections.ObjectModel;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Reporting.ViewModels;

public sealed class ReportingHomeViewModel : BindableBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<HistoryRow> HistoryRows { get; } = new();

    private string _selectedTagKey = string.Empty;
    public string SelectedTagKey { get => _selectedTagKey; set => SetProperty(ref _selectedTagKey, value); }

    private string _statusText = "输入标签 Key 后点击查询";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public DelegateCommand QueryCommand { get; }

    public ReportingHomeViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        QueryCommand = new DelegateCommand(async () => await QueryAsync());
    }

    private async Task QueryAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTagKey)) return;

        StatusText = "查询中...";
        HistoryRows.Clear();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KjDbContext>();
            var tagId = TagIdentity.GetTagId(SelectedTagKey);
            var rows = await db.TagHistory
                .Where(h => h.TagId == tagId)
                .OrderByDescending(h => h.Timestamp)
                .Take(500)
                .ToListAsync();

            foreach (var r in rows)
            {
                HistoryRows.Add(new HistoryRow
                {
                    Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    Value = r.Value ?? string.Empty,
                    Quality = r.Quality.ToString(),
                });
            }

            StatusText = $"查询完成，共 {HistoryRows.Count} 条记录";
        }
        catch (Exception ex)
        {
            StatusText = $"查询失败: {ex.Message}";
        }
    }
}

public sealed class HistoryRow
{
    public string Timestamp { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
}
