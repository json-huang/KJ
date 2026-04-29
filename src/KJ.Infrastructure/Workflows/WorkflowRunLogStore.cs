using System.Collections.Concurrent;
using KJ.Workflows;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.Infrastructure.Workflows;

public sealed class InMemoryWorkflowRunLogStore : IWorkflowRunLogStore
{
    private readonly ConcurrentQueue<WorkflowRunLogEntry> _q = new();
    private readonly int _max;

    public InMemoryWorkflowRunLogStore(int max = 2000) => _max = Math.Max(200, max);

    public void Append(WorkflowRunLogEntry entry)
    {
        _q.Enqueue(entry);
        while (_q.Count > _max && _q.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200)
    {
        take = Math.Clamp(take, 1, 2000);
        var arr = _q.ToArray();
        if (arr.Length <= take)
            return arr;
        return arr.Skip(Math.Max(0, arr.Length - take)).ToArray();
    }
}

/// <summary>
/// 最小落库实现：将 <see cref="WorkflowRunLogEntry"/> 写入 MySQL/SqlServer（取决于配置）。
/// 为避免 Runner 依赖 DbContext 生命周期，这里用 IServiceScopeFactory 按需创建 scope。
/// </summary>
public sealed class EfWorkflowRunLogStore : IWorkflowRunLogStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfWorkflowRunLogStore(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void Append(WorkflowRunLogEntry entry)
    {
        // fire-and-forget: 日志不应阻塞运行主循环；失败也不影响运行
        _ = PersistAsync(entry);
    }

    public IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KjDbContext>();

        var rows = db.WorkflowRunSteps
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new WorkflowRunLogEntry(
                new DateTimeOffset(DateTime.SpecifyKind(x.TimestampUtc, DateTimeKind.Utc)),
                x.RunId,
                x.StepId,
                x.Kind,
                x.Message,
                x.Success,
                x.Error))
            .ToArray();

        return rows.Reverse().ToArray();
    }

    private async Task PersistAsync(WorkflowRunLogEntry entry)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KjDbContext>();

            // ensure run row exists on first run-level event
            if (entry.StepId == Guid.Empty && entry.Kind == "Run" && entry.Message.StartsWith("Run started:", StringComparison.OrdinalIgnoreCase))
            {
                if (!await db.WorkflowRuns.AnyAsync(x => x.Id == entry.RunId).ConfigureAwait(false))
                {
                    db.WorkflowRuns.Add(new WorkflowRun
                    {
                        Id = entry.RunId,
                        WorkflowId = Guid.Empty,
                        WorkflowName = entry.Message,
                        StartedAtUtc = entry.Timestamp.UtcDateTime,
                        Success = false,
                    });
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
                return;
            }

            if (entry.StepId == Guid.Empty && entry.Kind == "Run" && entry.Message.StartsWith("Run completed", StringComparison.OrdinalIgnoreCase))
            {
                var run = await db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == entry.RunId).ConfigureAwait(false);
                if (run is not null)
                {
                    run.EndedAtUtc = entry.Timestamp.UtcDateTime;
                    run.Success = true;
                    run.Error = null;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
                return;
            }

            if (entry.StepId == Guid.Empty && entry.Kind == "Run" && entry.Message.StartsWith("Run failed", StringComparison.OrdinalIgnoreCase))
            {
                var run = await db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == entry.RunId).ConfigureAwait(false);
                if (run is not null)
                {
                    run.EndedAtUtc = entry.Timestamp.UtcDateTime;
                    run.Success = false;
                    run.Error = entry.Error;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
                return;
            }

            db.WorkflowRunSteps.Add(new WorkflowRunStep
            {
                Id = Guid.NewGuid(),
                RunId = entry.RunId,
                StepId = entry.StepId,
                Kind = entry.Kind,
                TimestampUtc = entry.Timestamp.UtcDateTime,
                Success = entry.Success,
                Message = entry.Message,
                Error = entry.Error,
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // best-effort logging only
        }
    }
}

