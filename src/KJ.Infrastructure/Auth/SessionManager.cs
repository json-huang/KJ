using System.Collections.Concurrent;

namespace KJ.Infrastructure.Auth;

/// <summary>
/// 会话管理服务。管理用户登录会话，支持强制下线。
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();

    /// <summary>用户登录时创建会话。</summary>
    public string CreateSession(string userId, string email)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            Email = email,
            CreatedAt = DateTimeOffset.Now,
            LastActiveAt = DateTimeOffset.Now,
            IsActive = true,
        };

        _sessions[sessionId] = session;
        return sessionId;
    }

    /// <summary>验证会话是否有效。</summary>
    public bool ValidateSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        if (!session.IsActive)
            return false;

        // 会话超时：24 小时不活动
        if (DateTimeOffset.Now - session.LastActiveAt > TimeSpan.FromHours(24))
        {
            session.IsActive = false;
            return false;
        }

        session.LastActiveAt = DateTimeOffset.Now;
        return true;
    }

    /// <summary>获取会话信息。</summary>
    public UserSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>强制下线。</summary>
    public bool ForceLogout(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsActive = false;
            return true;
        }
        return false;
    }

    /// <summary>强制下线指定用户的所有会话。</summary>
    public int ForceLogoutUser(string userId)
    {
        var count = 0;
        foreach (var session in _sessions.Values.Where(s => s.UserId == userId && s.IsActive))
        {
            session.IsActive = false;
            count++;
        }
        return count;
    }

    /// <summary>获取所有活跃会话。</summary>
    public IReadOnlyList<UserSession> GetActiveSessions()
    {
        return _sessions.Values
            .Where(s => s.IsActive && ValidateSession(s.SessionId))
            .OrderByDescending(s => s.LastActiveAt)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>登出。</summary>
    public void Logout(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.IsActive = false;
    }

    /// <summary>清理过期会话。</summary>
    public int CleanupExpired()
    {
        var expired = _sessions.Values
            .Where(s => !s.IsActive || DateTimeOffset.Now - s.LastActiveAt > TimeSpan.FromHours(24))
            .ToList();

        foreach (var session in expired)
            _sessions.TryRemove(session.SessionId, out _);

        return expired.Count;
    }
}

public sealed class UserSession
{
    public string SessionId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public bool IsActive { get; set; }
}
