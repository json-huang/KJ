using FluentAssertions;
using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class AuditLoggerTests : IDisposable
{
    private readonly KjDbContext _db;
    private readonly AuditLogger _sut;

    public AuditLoggerTests()
    {
        var options = new DbContextOptionsBuilder<KjDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _db = new KjDbContext(options);
        _sut = new AuditLogger(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task LogAsync_ShouldStoreEntryToDatabase()
    {
        var entry = new AuditEntry("user1", "Login", "Successful login", DateTimeOffset.UtcNow);

        await _sut.LogAsync(entry);

        var logs = await _db.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].UserId.Should().Be("user1");
        logs[0].Action.Should().Be("Login");
        logs[0].Details.Should().Be("Successful login");
    }

    [Fact]
    public async Task GetLogsAsync_ShouldRetrieveEntriesByTimeRange()
    {
        var now = DateTimeOffset.UtcNow;
        var entry1 = new AuditEntry("user1", "Action1", "Details1", now.AddHours(-2));
        var entry2 = new AuditEntry("user2", "Action2", "Details2", now.AddHours(-1));
        var entry3 = new AuditEntry("user3", "Action3", "Details3", now);

        await _sut.LogAsync(entry1);
        await _sut.LogAsync(entry2);
        await _sut.LogAsync(entry3);

        var logs = await _sut.GetLogsAsync(now.AddHours(-3), now.AddMinutes(1));

        logs.Should().HaveCount(3);
        logs.Should().BeInDescendingOrder(l => l.Timestamp);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldReturnEmpty_WhenNoEntriesInRange()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new AuditEntry("user1", "Action1", "Details1", now);

        await _sut.LogAsync(entry);

        var logs = await _sut.GetLogsAsync(now.AddDays(1), now.AddDays(2));

        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByTimeRange()
    {
        var now = DateTimeOffset.UtcNow;
        var entryOld = new AuditEntry("user1", "OldAction", "Old", now.AddHours(-5));
        var entryInRange = new AuditEntry("user2", "InRange", "In range", now.AddHours(-1));
        var entryNew = new AuditEntry("user3", "NewAction", "New", now.AddHours(5));

        await _sut.LogAsync(entryOld);
        await _sut.LogAsync(entryInRange);
        await _sut.LogAsync(entryNew);

        var logs = await _sut.GetLogsAsync(now.AddHours(-2), now);

        logs.Should().HaveCount(1);
        logs[0].Action.Should().Be("InRange");
    }

    [Fact]
    public async Task LogAsync_ShouldHandleNullDetails()
    {
        var entry = new AuditEntry("user1", "Action", null, DateTimeOffset.UtcNow);

        await _sut.LogAsync(entry);

        var logs = await _db.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].Details.Should().BeNull();
    }
}
