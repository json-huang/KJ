using System.Text.Json;
using KJ.Workflows;

namespace KJ.Modules.Monitoring.Workflows;

public interface IWorkflowStore
{
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDefinition?> LoadAutosaveAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAutosaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteAutosaveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasNewerAutosaveAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkEditorSessionOpenAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task MarkEditorSessionClosedAsync(CancellationToken cancellationToken = default);
    Task<Guid?> GetLastUnclosedEditorSessionWorkflowIdAsync(CancellationToken cancellationToken = default);
}

public sealed class WorkflowJsonStore : IWorkflowStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _dir;

    public WorkflowJsonStore()
    {
        // Use LocalAppData so it works for unpackaged/dev runs.
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dir = Path.Combine(baseDir, "KJ", "workflows");
        Directory.CreateDirectory(_dir);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(_dir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(x => !x.EndsWith(".autosave.json", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.EndsWith("editor-session.json", StringComparison.OrdinalIgnoreCase));
        var list = new List<WorkflowDefinition>();

        foreach (var f in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(f, cancellationToken).ConfigureAwait(false);
                var wf = JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
                if (wf is not null)
                    list.Add(wf);
            }
            catch
            {
                // ignore broken file
            }
        }

        return list
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<WorkflowDefinition?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
    }

    public async Task SaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        workflow.UpdatedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(GetPath(workflow.Id), json, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<WorkflowDefinition?> LoadAutosaveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetAutosavePath(id);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
    }

    public async Task SaveAutosaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        workflow.UpdatedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(workflow, JsonOptions);
        await File.WriteAllTextAsync(GetAutosavePath(workflow.Id), json, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAutosaveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetAutosavePath(id);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> HasNewerAutosaveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var autosave = GetAutosavePath(id);
        if (!File.Exists(autosave))
            return Task.FromResult(false);

        var official = GetPath(id);
        if (!File.Exists(official))
            return Task.FromResult(true);

        var autosaveUtc = File.GetLastWriteTimeUtc(autosave);
        var officialUtc = File.GetLastWriteTimeUtc(official);
        return Task.FromResult(autosaveUtc > officialUtc);
    }

    public async Task MarkEditorSessionOpenAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var path = GetEditorSessionPath();
        var json = JsonSerializer.Serialize(new EditorSessionMarker(workflowId, DateTimeOffset.UtcNow), JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public Task MarkEditorSessionClosedAsync(CancellationToken cancellationToken = default)
    {
        var path = GetEditorSessionPath();
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<Guid?> GetLastUnclosedEditorSessionWorkflowIdAsync(CancellationToken cancellationToken = default)
    {
        var path = GetEditorSessionPath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var marker = JsonSerializer.Deserialize<EditorSessionMarker>(json, JsonOptions);
            return marker?.WorkflowId;
        }
        catch
        {
            return null;
        }
    }

    private string GetPath(Guid id) => Path.Combine(_dir, $"{id:N}.json");
    private string GetAutosavePath(Guid id) => Path.Combine(_dir, $"{id:N}.autosave.json");
    private string GetEditorSessionPath() => Path.Combine(_dir, "editor-session.json");

    private sealed record EditorSessionMarker(Guid WorkflowId, DateTimeOffset OpenedAtUtc);
}

