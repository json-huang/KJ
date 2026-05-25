using FluentAssertions;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using KJ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class DataRetentionManagerTests : IDisposable
{
    private readonly KjDbContext _db;
    private readonly DataRetentionManager _sut;
    private readonly DbContextOptions<KjDbContext> _options;

    public DataRetentionManagerTests()
    {
        _options = new DbContextOptionsBuilder<KjDbContext>()
            .UseInMemoryDatabase($"RetentionTestDb_{Guid.NewGuid()}")
            .Options;

        _db = new KjDbContext(_options);
        var factory = new TestDbContextFactory(_options);
        _sut = new DataRetentionManager(factory);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void AddPolicy_ShouldAddNewPolicy()
    {
        var policy = new RetentionPolicy("CustomPolicy", 60);

        _sut.AddPolicy(policy);

        var result = _sut.GetPolicy("CustomPolicy");
        result.Should().NotBeNull();
        result!.RetentionDays.Should().Be(60);
    }

    [Fact]
    public void AddPolicy_ShouldUpdateExistingPolicy()
    {
        var policy1 = new RetentionPolicy("TagHistory", 30);
        var policy2 = new RetentionPolicy("TagHistory", 60);

        _sut.AddPolicy(policy1);
        _sut.AddPolicy(policy2);

        var result = _sut.GetPolicy("TagHistory");
        result.Should().NotBeNull();
        result!.RetentionDays.Should().Be(60);
    }

    [Fact]
    public void GetPolicies_ShouldReturnDefaultPolicies()
    {
        var policies = _sut.GetPolicies();

        policies.Should().HaveCount(3);
        policies.Should().Contain(p => p.Name == "TagHistory");
        policies.Should().Contain(p => p.Name == "AlarmHistory");
        policies.Should().Contain(p => p.Name == "AuditLog");
    }

    [Fact]
    public void GetPolicy_ShouldReturnNull_WhenNotFound()
    {
        var result = _sut.GetPolicy("NonExistent");

        result.Should().BeNull();
    }

    [Fact]
    public void RemovePolicy_ShouldRemoveExistingPolicy()
    {
        _sut.AddPolicy(new RetentionPolicy("TempPolicy", 10));

        var removed = _sut.RemovePolicy("TempPolicy");

        removed.Should().BeTrue();
        _sut.GetPolicy("TempPolicy").Should().BeNull();
    }

    [Fact]
    public void RemovePolicy_ShouldReturnFalse_WhenNotFound()
    {
        var removed = _sut.RemovePolicy("NonExistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredTagHistory()
    {
        // Arrange: 插入过期和未过期的 TagHistory
        var tag = new Tag { Id = Guid.NewGuid(), Name = "TestTag", DataType = TagDataType.Bool, PollIntervalMs = 1000 };
        _db.Tags.Add(tag);

        var oldHistory = new TagHistory
        {
            Id = Guid.NewGuid(),
            TagId = tag.Id,
            Timestamp = DateTime.UtcNow.AddDays(-60),
            Value = "100",
            Quality = QualityCode.Good
        };
        var newHistory = new TagHistory
        {
            Id = Guid.NewGuid(),
            TagId = tag.Id,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            Value = "200",
            Quality = QualityCode.Good
        };
        _db.TagHistory.AddRange(oldHistory, newHistory);
        await _db.SaveChangesAsync();

        // Act: 使用默认 30 天策略清理
        var result = await _sut.CleanupAsync("TagHistory");

        // Assert
        result.DeletedCount.Should().Be(1);
        result.Error.Should().BeNull();

        var remaining = await _db.TagHistory.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Value.Should().Be("200");
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredAlarmHistory()
    {
        // Arrange: 插入过期告警历史
        var alarm = new Alarm
        {
            Id = Guid.NewGuid(),
            Name = "TestAlarm",
            TagId = Guid.NewGuid(),
            Condition = AlarmCondition.GreaterThan,
            Level = AlarmLevel.Warning,
            IsEnabled = true
        };
        _db.Alarms.Add(alarm);

        var oldAlarm = new AlarmHistory
        {
            Id = Guid.NewGuid(),
            AlarmId = alarm.Id,
            Timestamp = DateTime.UtcNow.AddDays(-100),
            EventType = "Triggered",
            Message = "Old alarm"
        };
        var newAlarm = new AlarmHistory
        {
            Id = Guid.NewGuid(),
            AlarmId = alarm.Id,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            EventType = "Triggered",
            Message = "New alarm"
        };
        _db.AlarmHistory.AddRange(oldAlarm, newAlarm);
        await _db.SaveChangesAsync();

        // Act: 使用默认 90 天策略清理
        var result = await _sut.CleanupAsync("AlarmHistory");

        // Assert
        result.DeletedCount.Should().Be(1);

        var remaining = await _db.AlarmHistory.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Message.Should().Be("New alarm");
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredAuditLogs()
    {
        // Arrange: 插入过期审计日志
        var oldLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-200),
            UserId = "user1",
            Action = "OldAction",
            Details = "Old log"
        };
        var newLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-1),
            UserId = "user2",
            Action = "NewAction",
            Details = "New log"
        };
        _db.AuditLogs.AddRange(oldLog, newLog);
        await _db.SaveChangesAsync();

        // Act: 使用默认 180 天策略清理
        var result = await _sut.CleanupAsync("AuditLog");

        // Assert
        result.DeletedCount.Should().Be(1);

        var remaining = await _db.AuditLogs.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Action.Should().Be("NewAction");
    }

    [Fact]
    public async Task CleanupAllAsync_ShouldRunAllPolicies()
    {
        // Arrange: 为每个策略插入一条过期数据
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Tag1", DataType = TagDataType.Bool, PollIntervalMs = 1000 };
        _db.Tags.Add(tag);
        _db.TagHistory.Add(new TagHistory
        {
            Id = Guid.NewGuid(),
            TagId = tag.Id,
            Timestamp = DateTime.UtcNow.AddDays(-60),
            Value = "1",
            Quality = QualityCode.Good
        });

        var alarm = new Alarm
        {
            Id = Guid.NewGuid(),
            Name = "Alarm1",
            TagId = tag.Id,
            Condition = AlarmCondition.GreaterThan,
            Level = AlarmLevel.Warning,
            IsEnabled = true
        };
        _db.Alarms.Add(alarm);
        _db.AlarmHistory.Add(new AlarmHistory
        {
            Id = Guid.NewGuid(),
            AlarmId = alarm.Id,
            Timestamp = DateTime.UtcNow.AddDays(-100),
            EventType = "Triggered"
        });

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-200),
            Action = "OldAction"
        });

        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.CleanupAllAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(r => r.PolicyName == "TagHistory" && r.DeletedCount == 1);
        results.Should().Contain(r => r.PolicyName == "AlarmHistory" && r.DeletedCount == 1);
        results.Should().Contain(r => r.PolicyName == "AuditLog" && r.DeletedCount == 1);
        results.Should().OnlyContain(r => r.Error == null);
    }

    [Fact]
    public async Task CleanupAsync_ShouldReturnZero_WhenNoExpiredData()
    {
        var result = await _sut.CleanupAsync("TagHistory");

        result.DeletedCount.Should().Be(0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CleanupAsync_ShouldThrow_WhenPolicyNotFound()
    {
        var act = () => _sut.CleanupAsync("NonExistent");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NonExistent*");
    }

    /// <summary>测试用 DbContext 工厂，每次创建新实例。</summary>
    private sealed class TestDbContextFactory : IDbContextFactory<KjDbContext>
    {
        private readonly DbContextOptions<KjDbContext> _options;
        public TestDbContextFactory(DbContextOptions<KjDbContext> options) => _options = options;
        public KjDbContext CreateDbContext() => new KjDbContext(_options);
        public ValueTask<KjDbContext> CreateDbContextAsync(CancellationToken ct = default) => ValueTask.FromResult(new KjDbContext(_options));
    }
}
