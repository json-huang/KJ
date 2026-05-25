namespace KJ.Domain.Services;

/// <summary>
/// 告警通知接口。支持多种通知渠道（邮件、短信、Webhook 等）。
/// </summary>
public interface IAlarmNotifier
{
    Task NotifyAsync(AlarmNotification notification, CancellationToken ct = default);
}

public sealed record AlarmNotification(
    string AlarmId,
    string RuleId,
    string TagKey,
    string Message,
    AlarmSeverity Severity,
    DateTimeOffset TriggeredAt,
    string? AcknowledgedBy = null);

/// <summary>
/// 告警通知聚合器。将告警分发到所有注册的通知渠道。
/// </summary>
public sealed class AlarmNotificationService : IDisposable
{
    private readonly IAlarmService _alarmService;
    private readonly List<IAlarmNotifier> _notifiers = new();
    private readonly object _gate = new();
    private bool _disposed;

    public AlarmNotificationService(IAlarmService alarmService)
    {
        _alarmService = alarmService;
        _alarmService.AlarmRaised += OnAlarmRaised;
    }

    public void AddNotifier(IAlarmNotifier notifier)
    {
        lock (_gate) _notifiers.Add(notifier);
    }

    private async void OnAlarmRaised(object? sender, AlarmEvent e)
    {
        if (_disposed) return;

        IAlarmNotifier[] snapshot;
        lock (_gate) snapshot = _notifiers.ToArray();

        var notification = new AlarmNotification(
            AlarmId: e.Code,
            RuleId: e.Code,
            TagKey: "",
            Message: e.Message,
            Severity: e.Severity,
            TriggeredAt: e.Timestamp);

        foreach (var notifier in snapshot)
        {
            try
            {
                await notifier.NotifyAsync(notification).ConfigureAwait(false);
            }
            catch
            {
                // best-effort: 通知失败不应影响主流程
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _alarmService.AlarmRaised -= OnAlarmRaised;

        lock (_gate)
        {
            foreach (var notifier in _notifiers)
            {
                (notifier as IDisposable)?.Dispose();
            }
            _notifiers.Clear();
        }
    }
}

/// <summary>
/// 日志通知器（用于调试和测试）。将告警写入内存列表。
/// </summary>
public sealed class LogAlarmNotifier : IAlarmNotifier
{
    private readonly List<AlarmNotification> _sent = new();
    private readonly object _gate = new();

    public IReadOnlyList<AlarmNotification> Sent { get { lock (_gate) return _sent.ToList().AsReadOnly(); } }

    public Task NotifyAsync(AlarmNotification notification, CancellationToken ct = default)
    {
        lock (_gate) _sent.Add(notification);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Webhook 通知器。通过 HTTP POST 发送告警到外部系统。
/// </summary>
public sealed class WebhookAlarmNotifier : IAlarmNotifier
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public WebhookAlarmNotifier(HttpClient httpClient, string webhookUrl)
    {
        _httpClient = httpClient;
        _webhookUrl = webhookUrl;
    }

    public async Task NotifyAsync(AlarmNotification notification, CancellationToken ct = default)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            alarmId = notification.AlarmId,
            message = notification.Message,
            severity = notification.Severity.ToString(),
            triggeredAt = notification.TriggeredAt.ToString("O"),
        });

        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(_webhookUrl, content, ct).ConfigureAwait(false);
    }
}
