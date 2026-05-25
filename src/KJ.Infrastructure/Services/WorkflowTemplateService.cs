using System.Text.Json;
using KJ.Workflows;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 工作流模板服务。支持将常用工作流保存为模板、从模板创建工作流。
/// </summary>
public sealed class WorkflowTemplateService
{
    private readonly string _templateDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public WorkflowTemplateService()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _templateDir = Path.Combine(baseDir, "KJ", "templates");
        Directory.CreateDirectory(_templateDir);
    }

    /// <summary>保存工作流为模板。</summary>
    public async Task SaveTemplateAsync(WorkflowDefinition workflow, string templateName, CancellationToken ct = default)
    {
        var template = new WorkflowTemplate
        {
            Id = Guid.NewGuid(),
            Name = templateName,
            Description = $"由 {workflow.Name} 创建",
            CreatedAt = DateTimeOffset.Now,
            Workflow = workflow,
        };

        var json = JsonSerializer.Serialize(template, JsonOptions);
        var path = Path.Combine(_templateDir, $"{template.Id:N}.json");
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    /// <summary>列出所有模板。</summary>
    public IReadOnlyList<WorkflowTemplateInfo> ListTemplates()
    {
        var files = Directory.EnumerateFiles(_templateDir, "*.json");
        var templates = new List<WorkflowTemplateInfo>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonSerializer.Deserialize<WorkflowTemplate>(json, JsonOptions);
                if (template is not null)
                {
                    templates.Add(new WorkflowTemplateInfo
                    {
                        Id = template.Id,
                        Name = template.Name,
                        Description = template.Description,
                        CreatedAt = template.CreatedAt,
                        StepCount = template.Workflow?.Steps?.Count ?? 0,
                    });
                }
            }
            catch
            {
                // 跳过损坏的模板文件
            }
        }

        return templates.OrderByDescending(t => t.CreatedAt).ToList().AsReadOnly();
    }

    /// <summary>从模板创建工作流（新 ID）。</summary>
    public async Task<WorkflowDefinition?> CreateFromTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var path = Path.Combine(_templateDir, $"{templateId:N}.json");
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var template = JsonSerializer.Deserialize<WorkflowTemplate>(json, JsonOptions);
        if (template?.Workflow is null) return null;

        var wf = template.Workflow;
        wf.Id = Guid.NewGuid();
        wf.Name = $"{template.Name} - 副本";
        wf.Version = 1;
        wf.UpdatedAt = DateTimeOffset.Now;

        // 重新映射步骤 ID
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var step in wf.Steps)
        {
            var oldId = step.Id;
            step.Id = Guid.NewGuid();
            idMap[oldId] = step.Id;
        }

        foreach (var step in wf.Steps)
        {
            if (step.NextStepId.HasValue && idMap.TryGetValue(step.NextStepId.Value, out var newNextId))
                step.NextStepId = newNextId;
            foreach (var branch in step.Branches)
            {
                if (idMap.TryGetValue(branch.NextStepId, out var newBranchNextId))
                    branch.NextStepId = newBranchNextId;
            }
        }

        return wf;
    }

    /// <summary>删除模板。</summary>
    public Task DeleteTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var path = Path.Combine(_templateDir, $"{templateId:N}.json");
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}

public sealed class WorkflowTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public WorkflowDefinition? Workflow { get; set; }
}

public sealed class WorkflowTemplateInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public int StepCount { get; set; }
}
