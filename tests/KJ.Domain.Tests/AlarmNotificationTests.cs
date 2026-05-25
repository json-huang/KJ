using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class AlarmNotificationServiceTests
{
    private static async Task WaitForNotifications(LogAlarmNotifier notifier, int count, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (notifier.Sent.Count >= count) return;
            await Task.Delay(50);
        }
        notifier.Sent.Count.Should().BeGreaterOrEqualTo(count, "expected notifications within timeout");
    }

    [Fact]
    public async Task AlarmRaised_ShouldNotifyAllNotifiers()
    {
        var alarmService = new AlarmService();
        var notifier1 = new LogAlarmNotifier();
        var notifier2 = new LogAlarmNotifier();
        var svc = new AlarmNotificationService(alarmService);
        svc.AddNotifier(notifier1);
        svc.AddNotifier(notifier2);

        alarmService.Raise(new AlarmEvent("r1", "High temp", AlarmSeverity.Warning, DateTimeOffset.Now));

        await WaitForNotifications(notifier1, 1);
        await WaitForNotifications(notifier2, 1);

        notifier1.Sent.Should().HaveCount(1);
        notifier2.Sent.Should().HaveCount(1);
    }

    [Fact]
    public void AlarmRaised_ShouldNotThrow_WhenNotifierFails()
    {
        var alarmService = new AlarmService();
        var svc = new AlarmNotificationService(alarmService);
        svc.AddNotifier(new FailingNotifier());

        var act = () => alarmService.Raise(new AlarmEvent("r1", "test", AlarmSeverity.Warning, DateTimeOffset.Now));
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AlarmRaised_ShouldStillDeliverToOtherNotifiers_WhenOneFails()
    {
        var alarmService = new AlarmService();
        var good = new LogAlarmNotifier();
        var svc = new AlarmNotificationService(alarmService);
        svc.AddNotifier(new FailingNotifier());
        svc.AddNotifier(good);

        alarmService.Raise(new AlarmEvent("r1", "test", AlarmSeverity.Warning, DateTimeOffset.Now));

        await WaitForNotifications(good, 1);

        good.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task Notification_ShouldContainAlarmInfo()
    {
        var alarmService = new AlarmService();
        var notifier = new LogAlarmNotifier();
        var svc = new AlarmNotificationService(alarmService);
        svc.AddNotifier(notifier);

        alarmService.Raise(new AlarmEvent("r1", "High temp", AlarmSeverity.Critical, DateTimeOffset.Now));

        await WaitForNotifications(notifier, 1);

        notifier.Sent.Should().ContainSingle();
        var n = notifier.Sent[0];
        n.Message.Should().Be("High temp");
        n.Severity.Should().Be(AlarmSeverity.Critical);
    }

    private sealed class FailingNotifier : IAlarmNotifier
    {
        public Task NotifyAsync(AlarmNotification notification, CancellationToken ct = default) =>
            throw new Exception("notification failed");
    }
}
