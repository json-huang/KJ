using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 定时报表服务。支持 cron 表达式或固定间隔定时生成 CSV 报表文件。
/// </summary>
public sealed class ScheduledReportService : IDisposable
{
    private readonly TagHistoryExportService _exportService;
    private readonly ILogger<ScheduledReportService>? _logger;
    private readonly List<ReportSchedule> _schedules = [];
    private readonly object _lock = new();
    private Timer? _timer;
    private bool _disposed;

    /// <summary>报表生成完成事件。参数为输出文件路径。</summary>
    public event Action<string>? ReportGenerated;

    public ScheduledReportService(
        TagHistoryExportService exportService,
        ILogger<ScheduledReportService>? logger = null)
    {
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>当前配置的定时报表数量。</summary>
    public int ScheduleCount
    {
        get { lock (_lock) return _schedules.Count; }
    }

    /// <summary>
    /// 添加固定间隔的定时报表。
    /// </summary>
    /// <param name="name">报表名称（用于文件命名）。</param>
    /// <param name="interval">生成间隔。</param>
    /// <param name="outputDirectory">输出目录。</param>
    /// <param name="tagFilter">标签名称过滤（可选）。</param>
    /// <param name="lookback">回溯时间范围。默认最近 1 小时。</param>
    public void AddIntervalSchedule(
        string name,
        TimeSpan interval,
        string outputDirectory,
        string? tagFilter = null,
        TimeSpan? lookback = null)
    {
        var schedule = new ReportSchedule
        {
            Name = name,
            OutputDirectory = outputDirectory,
            TagFilter = tagFilter,
            Lookback = lookback ?? TimeSpan.FromHours(1),
            Mode = ScheduleMode.Interval,
            Interval = interval,
        };

        lock (_lock)
        {
            _schedules.Add(schedule);
        }

        EnsureTimerRunning();
        _logger?.LogInformation("Added interval schedule '{Name}' every {Interval}", name, interval);
    }

    /// <summary>
    /// 添加 cron 表达式的定时报表。
    /// 支持格式: "分 时 日 月 周"（标准 5 字段 cron）。
    /// </summary>
    public void AddCronSchedule(
        string name,
        string cronExpression,
        string outputDirectory,
        string? tagFilter = null,
        TimeSpan? lookback = null)
    {
        var schedule = new ReportSchedule
        {
            Name = name,
            OutputDirectory = outputDirectory,
            TagFilter = tagFilter,
            Lookback = lookback ?? TimeSpan.FromHours(1),
            Mode = ScheduleMode.Cron,
            CronExpression = cronExpression,
        };

        ParseCron(cronExpression, schedule);

        lock (_lock)
        {
            _schedules.Add(schedule);
        }

        EnsureTimerRunning();
        _logger?.LogInformation("Added cron schedule '{Name}' with expression '{Cron}'", name, cronExpression);
    }

    /// <summary>移除指定名称的定时报表。</summary>
    public bool RemoveSchedule(string name)
    {
        lock (_lock)
        {
            return _schedules.RemoveAll(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    /// <summary>立即触发一次所有报表生成（用于手动测试）。</summary>
    public async Task GenerateAllReportsAsync(CancellationToken ct = default)
    {
        List<ReportSchedule> snapshot;
        lock (_lock)
        {
            snapshot = [.. _schedules];
        }

        foreach (var schedule in snapshot)
        {
            await GenerateReportAsync(schedule, ct).ConfigureAwait(false);
        }
    }

    /// <summary>立即触发指定报表生成。</summary>
    public async Task GenerateReportAsync(string name, CancellationToken ct = default)
    {
        ReportSchedule? schedule;
        lock (_lock)
        {
            schedule = _schedules.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (schedule is null)
            throw new ArgumentException($"Schedule '{name}' not found.", nameof(name));

        await GenerateReportAsync(schedule, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }

    #region Timer & Scheduling

    private void EnsureTimerRunning()
    {
        if (_timer is not null) return;

        // 每 30 秒检查一次是否有需要执行的调度
        _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private async void OnTimerTick(object? state)
    {
        if (_disposed) return;

        var now = DateTimeOffset.Now;
        List<ReportSchedule> toExecute = [];

        lock (_lock)
        {
            foreach (var schedule in _schedules)
            {
                if (!schedule.ShouldRun(now)) continue;
                schedule.LastRun = now;
                toExecute.Add(schedule);
            }
        }

        foreach (var schedule in toExecute)
        {
            try
            {
                await GenerateReportAsync(schedule).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to generate report '{Name}'", schedule.Name);
            }
        }
    }

    #endregion

    #region Report Generation

    private async Task GenerateReportAsync(ReportSchedule schedule, CancellationToken ct = default)
    {
        var to = DateTimeOffset.Now;
        var from = to - schedule.Lookback;

        _logger?.LogInformation("Generating report '{Name}' for {From} ~ {To}",
            schedule.Name, from, to);

        var csv = await _exportService.ExportToCsvAsync(from, to, schedule.TagFilter, ct)
            .ConfigureAwait(false);

        Directory.CreateDirectory(schedule.OutputDirectory);

        var fileName = $"{schedule.Name}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(schedule.OutputDirectory, fileName);

        await File.WriteAllTextAsync(filePath, csv, Encoding.UTF8, ct).ConfigureAwait(false);

        _logger?.LogInformation("Report saved to {Path}", filePath);
        ReportGenerated?.Invoke(filePath);
    }

    #endregion

    #region Cron Parsing

    /// <summary>解析 5 字段 cron 表达式。</summary>
    private static void ParseCron(string expression, ReportSchedule schedule)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new ArgumentException(
                $"Cron expression must have 5 fields (min hour day month weekday), got {parts.Length}.",
                nameof(expression));

        schedule.CronMinutes = ParseCronField(parts[0], 0, 59);
        schedule.CronHours = ParseCronField(parts[1], 0, 23);
        schedule.CronDays = ParseCronField(parts[2], 1, 31);
        schedule.CronMonths = ParseCronField(parts[3], 1, 12);
        schedule.CronWeekdays = ParseCronField(parts[4], 0, 6); // 0=Sunday
    }

    private static HashSet<int> ParseCronField(string field, int min, int max)
    {
        var result = new HashSet<int>();

        // 通配符
        if (field == "*")
        {
            for (int i = min; i <= max; i++) result.Add(i);
            return result;
        }

        // 逗号分隔: "1,3,5"
        foreach (var part in field.Split(','))
        {
            // 范围: "1-5"
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);
                for (int i = start; i <= end; i++) result.Add(i);
            }
            // 步长: "*/5" 或 "1/2"
            else if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                int step = int.Parse(stepParts[1]);
                int start = stepParts[0] == "*" ? min : int.Parse(stepParts[0]);
                for (int i = start; i <= max; i += step) result.Add(i);
            }
            else
            {
                result.Add(int.Parse(part));
            }
        }

        return result;
    }

    #endregion
}

#region Internal Models

internal enum ScheduleMode
{
    Interval,
    Cron,
}

internal sealed class ReportSchedule
{
    public string Name { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string? TagFilter { get; set; }
    public TimeSpan Lookback { get; set; }
    public ScheduleMode Mode { get; set; }

    // Interval mode
    public TimeSpan Interval { get; set; }

    // Cron mode
    public string? CronExpression { get; set; }
    public HashSet<int> CronMinutes { get; set; } = [];
    public HashSet<int> CronHours { get; set; } = [];
    public HashSet<int> CronDays { get; set; } = [];
    public HashSet<int> CronMonths { get; set; } = [];
    public HashSet<int> CronWeekdays { get; set; } = [];

    public DateTimeOffset? LastRun { get; set; }

    public bool ShouldRun(DateTimeOffset now)
    {
        return Mode switch
        {
            ScheduleMode.Interval => ShouldRunInterval(now),
            ScheduleMode.Cron => ShouldRunCron(now),
            _ => false,
        };
    }

    private bool ShouldRunInterval(DateTimeOffset now)
    {
        if (!LastRun.HasValue) return true;
        return now - LastRun.Value >= Interval;
    }

    private bool ShouldRunCron(DateTimeOffset now)
    {
        // 检查是否匹配 cron 字段
        if (!CronMinutes.Contains(now.Minute)) return false;
        if (!CronHours.Contains(now.Hour)) return false;
        if (!CronDays.Contains(now.Day)) return false;
        if (!CronMonths.Contains(now.Month)) return false;

        var weekday = now.DayOfWeek == DayOfWeek.Sunday ? 0 : (int)now.DayOfWeek;
        if (!CronWeekdays.Contains(weekday)) return false;

        // 避免同一分钟内重复执行
        if (LastRun.HasValue && (now - LastRun.Value).TotalSeconds < 60)
            return false;

        return true;
    }
}

#endregion
