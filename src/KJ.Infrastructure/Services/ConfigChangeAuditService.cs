using System.Collections.Concurrent;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 配置变更审计服务。记录设备、标签、工作流等配置的变更历史。
/// </summary>
public sealed class ConfigChangeAuditService
{
    private readonly ConcurrentBag<ConfigChangeEntry> _changes = new();

    /// <summary>记录一条配置变更。</summary>
    public void RecordChange(
        string userId,
        ConfigChangeType changeType,
        string targetName,
        string? oldValue = null,
        string? newValue = null,
        string? details = null)
    {
        _changes.Add(new ConfigChangeEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChangeType = changeType,
            TargetName = targetName,
            OldValue = oldValue,
            NewValue = newValue,
            Details = details,
            Timestamp = DateTimeOffset.Now,
        });
    }

    /// <summary>查询变更历史，支持按时间、用户、变更类型过滤。</summary>
    public IReadOnlyList<ConfigChangeEntry> QueryChanges(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? userId = null,
        ConfigChangeType? changeType = null,
        int limit = 500)
    {
        var query = _changes.AsEnumerable();

        if (from.HasValue)
            query = query.Where(c => c.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(c => c.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(c => c.UserId == userId);
        if (changeType.HasValue)
            query = query.Where(c => c.ChangeType == changeType.Value);

        return query
            .OrderByDescending(c => c.Timestamp)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>获取变更详情，包含前后对比。</summary>
    public ConfigChangeDiff? GetChangeDiff(Guid changeId)
    {
        var entry = _changes.FirstOrDefault(c => c.Id == changeId);
        if (entry is null) return null;

        return new ConfigChangeDiff
        {
            Entry = entry,
            HasChanges = entry.OldValue != entry.NewValue,
            Summary = BuildDiffSummary(entry),
        };
    }

    /// <summary>获取指定目标的所有变更历史。</summary>
    public IReadOnlyList<ConfigChangeEntry> GetTargetHistory(string targetName, int limit = 100)
    {
        return _changes
            .Where(c => c.TargetName == targetName)
            .OrderByDescending(c => c.Timestamp)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>获取变更统计。</summary>
    public ConfigChangeStats GetStats(DateTimeOffset from, DateTimeOffset to)
    {
        var entries = _changes
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .ToList();

        var byType = entries
            .GroupBy(c => c.ChangeType)
            .ToDictionary(g => g.Key, g => g.Count());

        var byUser = entries
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ConfigChangeStats(entries.Count, byType, byUser, from, to);
    }

    private static string BuildDiffSummary(ConfigChangeEntry entry)
    {
        return entry switch
        {
            { OldValue: null, NewValue: not null } => $"新增: {entry.NewValue}",
            { OldValue: not null, NewValue: null } => $"删除: {entry.OldValue}",
            { OldValue: not null, NewValue: not null } => $"变更: {entry.OldValue} → {entry.NewValue}",
            _ => entry.Details ?? "无详细信息",
        };
    }
}

/// <summary>配置变更类型。</summary>
public enum ConfigChangeType
{
    /// <summary>设备添加</summary>
    DeviceAdded,
    /// <summary>设备删除</summary>
    DeviceRemoved,
    /// <summary>设备修改</summary>
    DeviceModified,
    /// <summary>标签添加</summary>
    TagAdded,
    /// <summary>标签删除</summary>
    TagRemoved,
    /// <summary>标签修改</summary>
    TagModified,
    /// <summary>工作流创建</summary>
    WorkflowCreated,
    /// <summary>工作流修改</summary>
    WorkflowModified,
    /// <summary>工作流删除</summary>
    WorkflowDeleted,
    /// <summary>其他配置变更</summary>
    Other,
}

/// <summary>配置变更记录条目。</summary>
public sealed class ConfigChangeEntry
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public ConfigChangeType ChangeType { get; set; }
    public string TargetName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>变更前后对比结果。</summary>
public sealed class ConfigChangeDiff
{
    public ConfigChangeEntry Entry { get; set; } = null!;
    public bool HasChanges { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>变更统计信息。</summary>
public sealed record ConfigChangeStats(
    int TotalChanges,
    IReadOnlyDictionary<ConfigChangeType, int> ByType,
    IReadOnlyDictionary<string, int> ByUser,
    DateTimeOffset From,
    DateTimeOffset To);
