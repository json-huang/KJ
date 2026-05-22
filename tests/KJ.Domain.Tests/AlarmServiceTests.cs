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
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "test", true, HighThreshold: 1));
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
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "test", true, HighThreshold: 1));
        svc.Evaluate("temp", 1);

        var active = svc.GetActiveAlarms();
        active.Should().HaveCount(1);

        svc.ClearAlarm(active[0].Id);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    // ── Threshold validation tests ────────────────────────────────────────

    [Fact]
    public void Evaluate_GreaterThan_ShouldTrigger_WhenValueExceedsHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true, HighThreshold: 100));

        svc.Evaluate("temp", 150);

        svc.GetActiveAlarms().Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_GreaterThan_ShouldNotTrigger_WhenValueBelowHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true, HighThreshold: 100));

        svc.Evaluate("temp", 50);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_LessThan_ShouldTrigger_WhenValueBelowLowThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.LessThan, AlarmSeverity.Warning, "Low temp", true, LowThreshold: 10));

        svc.Evaluate("temp", 5);

        svc.GetActiveAlarms().Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_LessThan_ShouldNotTrigger_WhenValueAboveLowThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.LessThan, AlarmSeverity.Warning, "Low temp", true, LowThreshold: 10));

        svc.Evaluate("temp", 50);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_Equals_ShouldTrigger_WhenValueMatchesHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "Exact match", true, HighThreshold: 42));

        svc.Evaluate("temp", 42);

        svc.GetActiveAlarms().Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_Equals_ShouldNotTrigger_WhenValueDiffersFromHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "Exact match", true, HighThreshold: 42));

        svc.Evaluate("temp", 43);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_NotEquals_ShouldTrigger_WhenValueDiffersFromHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.NotEquals, AlarmSeverity.Warning, "Not equal", true, HighThreshold: 42));

        svc.Evaluate("temp", 43);

        svc.GetActiveAlarms().Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_NotEquals_ShouldNotTrigger_WhenValueMatchesHighThreshold()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.NotEquals, AlarmSeverity.Warning, "Not equal", true, HighThreshold: 42));

        svc.Evaluate("temp", 42);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenValueIsNull()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "test", true, HighThreshold: 100));

        svc.Evaluate("temp", null);

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenValueIsNonNumeric()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "test", true, HighThreshold: 100));

        svc.Invoking(s => s.Evaluate("temp", "not_a_number"))
           .Should().NotThrow();

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenRuleIsDisabled()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "test", false, HighThreshold: 100));

        svc.Evaluate("temp", 150);

        svc.GetActiveAlarms().Should().BeEmpty();
    }
}
