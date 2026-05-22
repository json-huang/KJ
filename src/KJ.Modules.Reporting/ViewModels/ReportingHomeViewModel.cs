using System.Collections.ObjectModel;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Mvvm;
using Windows.Storage.Pickers;

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
    public DelegateCommand ExportCsvCommand { get; }

    /// <summary>
    /// 由 View 设置，用于初始化文件选取器的父窗口句柄。
    /// </summary>
    public nint ParentHwnd { get; set; }

    public ReportingHomeViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        QueryCommand = new DelegateCommand(async () => await QueryAsync());
        ExportCsvCommand = new DelegateCommand(async () => await ExportCsvAsync());
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

    private async Task ExportCsvAsync()
    {
        if (HistoryRows.Count == 0)
        {
            StatusText = "没有可导出的数据，请先查询";
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, ParentHwnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("CSV 文件", new[] { ".csv" });
            picker.SuggestedFileName = $"History_{SelectedTagKey}_{DateTime.Now:yyyyMMdd_HHmmss}";

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            StatusText = "正在导出...";

            await using var stream = await file.OpenStreamForWriteAsync();
            await using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);

            await writer.WriteLineAsync("Timestamp,Value,Quality");

            foreach (var row in HistoryRows)
            {
                var line = $"{EscapeCsv(row.Timestamp)},{EscapeCsv(row.Value)},{EscapeCsv(row.Quality)}";
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync();

            StatusText = $"导出完成: {file.Name} ({HistoryRows.Count} 条记录)";
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败: {ex.Message}";
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

public sealed class HistoryRow
{
    public string Timestamp { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
}
