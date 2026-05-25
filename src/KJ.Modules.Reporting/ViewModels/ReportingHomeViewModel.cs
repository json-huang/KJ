using System.Collections.ObjectModel;
using KJ.Infrastructure.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Reporting.ViewModels;

/// <summary>
/// 报表主页 ViewModel。查询标签历史并导出 CSV。
/// </summary>
public sealed class ReportingHomeViewModel : BindableBase
{
    private readonly TagHistoryExportService _exportService;

    public ObservableCollection<HistoryRow> HistoryRows { get; } = new();

    private string _selectedTagKey = string.Empty;
    public string SelectedTagKey { get => _selectedTagKey; set => SetProperty(ref _selectedTagKey, value); }

    private DateTimeOffset _fromDate = DateTimeOffset.Now.AddDays(-7);
    public DateTimeOffset FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }

    private DateTimeOffset _toDate = DateTimeOffset.Now;
    public DateTimeOffset ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    private string _statusText = "输入标签 Key 后点击查询";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private byte[]? _lastExportBytes;
    public bool HasExportData => _lastExportBytes is not null;

    public DelegateCommand QueryCommand { get; }
    public DelegateCommand ExportCsvCommand { get; }

    public ReportingHomeViewModel(TagHistoryExportService exportService)
    {
        _exportService = exportService;
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
            var csv = await _exportService.ExportToCsvAsync(FromDate, ToDate, SelectedTagKey);
            _lastExportBytes = System.Text.Encoding.UTF8.GetBytes(csv);

            // 解析 CSV 行（跳过表头）
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = ParseCsvLine(lines[i]);
                if (parts.Length >= 3)
                {
                    HistoryRows.Add(new HistoryRow
                    {
                        Timestamp = parts[0],
                        Value = parts[1],
                        Quality = parts.Length > 2 ? parts[2] : "",
                    });
                }
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
        if (_lastExportBytes is null)
        {
            StatusText = "没有可导出的数据，请先查询";
            return;
        }

        try
        {
            // 保存到默认路径（跨平台兼容，不依赖 WinUI FileSavePicker）
            var fileName = $"History_{SelectedTagKey}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                fileName);

            await File.WriteAllBytesAsync(filePath, _lastExportBytes);
            StatusText = $"导出完成: {filePath} ({HistoryRows.Count} 条记录)";
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败: {ex.Message}";
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { parts.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        parts.Add(current.ToString());
        return parts.ToArray();
    }
}

public sealed class HistoryRow
{
    public string Timestamp { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
}
