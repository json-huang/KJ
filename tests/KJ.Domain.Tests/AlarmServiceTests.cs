using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class AlarmServiceTests
{
    [Fact]
    public void AddRule_ShouldStoreRule()
    {
        var svc = new AlarmService();
        var rule = new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true);

        svc.AddRule(rule);

        svc.GetRules().Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRule_ShouldRemoveRule()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true));

        svc.RemoveRule("r1");

        svc.GetRules().Should().BeEmpty();
    }

    [Fact]
    public void Raise_ShouldFireAlarmRaisedEvent()
    {
        var svc = new AlarmService();
        AlarmEvent? received = null;
        svc.AlarmRaised += (_, e) => received = e;

        var evt = new AlarmEvent("code", "msg", AlarmSeverity.Warning, DateTimeOffset.Now);
        svc.Raise(evt);

        received.Should().NotBeNull();
        received!.Code.Should().Be("code");
    }

    [Fact]
    public void AcknowledgeAlarm_ShouldMarkAsAcknowledged()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "test", true));
        svc.Evaluate("temp", 1);

        var active = svc.GetActiveAlarms();
        active.Should().HaveCount(1);

        svc.AcknowledgeAlarm(active[0].Id, "user1");

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveAlarms_ShouldReturnEmpty_WhenNone()
    {
        var svc = new AlarmService();
        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void ClearAlarm_ShouldRemoveAlarm()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "test", true));
        svc.Evaluate("temp", 1);

        var active = svc.GetActiveAlarms();
        active.Should().HaveCount(1);

        svc.ClearAlarm(active[0].Id);

        svc.GetActiveAlarms().Should().BeEmpty();
    }
}
