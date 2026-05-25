using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Infrastructure.Auth;
using KJ.Infrastructure.Services;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class SessionManagerTests
{
    [Fact]
    public void CreateSession_ShouldReturnSessionId()
    {
        var mgr = new SessionManager();
        var id = mgr.CreateSession("user1", "test@example.com");

        id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidateSession_ShouldReturnTrue_ForActiveSession()
    {
        var mgr = new SessionManager();
        var id = mgr.CreateSession("user1", "test@example.com");

        mgr.ValidateSession(id).Should().BeTrue();
    }

    [Fact]
    public void ValidateSession_ShouldReturnFalse_ForInvalidSession()
    {
        var mgr = new SessionManager();
        mgr.ValidateSession("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void GetSession_ShouldReturnSessionInfo()
    {
        var mgr = new SessionManager();
        var id = mgr.CreateSession("user1", "test@example.com");

        var session = mgr.GetSession(id);

        session.Should().NotBeNull();
        session!.UserId.Should().Be("user1");
        session.Email.Should().Be("test@example.com");
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ForceLogout_ShouldDeactivateSession()
    {
        var mgr = new SessionManager();
        var id = mgr.CreateSession("user1", "test@example.com");

        mgr.ForceLogout(id).Should().BeTrue();
        mgr.ValidateSession(id).Should().BeFalse();
    }

    [Fact]
    public void ForceLogout_ShouldReturnFalse_ForInvalidSession()
    {
        var mgr = new SessionManager();
        mgr.ForceLogout("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void ForceLogoutUser_ShouldLogoutAllUserSessions()
    {
        var mgr = new SessionManager();
        mgr.CreateSession("user1", "a@test.com");
        mgr.CreateSession("user1", "b@test.com");
        mgr.CreateSession("user2", "c@test.com");

        var count = mgr.ForceLogoutUser("user1");

        count.Should().Be(2);
        mgr.GetActiveSessions().Should().ContainSingle();
    }

    [Fact]
    public void GetActiveSessions_ShouldReturnOnlyActiveSessions()
    {
        var mgr = new SessionManager();
        var id1 = mgr.CreateSession("user1", "a@test.com");
        var id2 = mgr.CreateSession("user2", "b@test.com");
        mgr.ForceLogout(id1);

        var active = mgr.GetActiveSessions();

        active.Should().ContainSingle();
        active[0].SessionId.Should().Be(id2);
    }

    [Fact]
    public void Logout_ShouldDeactivateSession()
    {
        var mgr = new SessionManager();
        var id = mgr.CreateSession("user1", "test@example.com");

        mgr.Logout(id);

        mgr.ValidateSession(id).Should().BeFalse();
    }

    [Fact]
    public void CleanupExpired_ShouldRemoveInactiveSessions()
    {
        var mgr = new SessionManager();
        mgr.CreateSession("user1", "a@test.com");
        var id2 = mgr.CreateSession("user2", "b@test.com");
        mgr.ForceLogout(id2);

        var cleaned = mgr.CleanupExpired();

        cleaned.Should().Be(1);
    }
}
