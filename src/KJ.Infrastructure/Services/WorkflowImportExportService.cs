using System.Text.Json;
using KJ.Workflows;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 工作流导入导出服务。支持 JSON 格式的工作流定义导入导出。
/// </summary>
public sealed class WorkflowImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>导出工作流为 JSON 字符串。</summary>
    public string Export(WorkflowDefinition workflow)
    {
        return JsonSerializer.Serialize(workflow, JsonOptions);
    }

    /// <summary>导出工作流为 JSON 字节数组（用于文件下载）。</summary>
    public byte[] ExportToBytes(WorkflowDefinition workflow)
    {
        return JsonSerializer.SerializeToUtf8Bytes(workflow, JsonOptions);
    }

    /// <summary>从 JSON 字符串导入工作流。返回新的 WorkflowDefinition（新 ID）。</summary>
    public WorkflowDefinition? Import(string json)
    {
        try
        {
            var wf = JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
            if (wf is null) return null;

            // 生成新 ID，避免冲突
            wf.Id = Guid.NewGuid();
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

            // 更新 NextStepId 引用
            foreach (var step in wf.Steps)
            {
                if (step.NextStepId.HasValue && idMap.TryGetValue(step.NextStepId.Value, out var newNextId))
                    step.NextStepId = newNextId;

                // 更新 Branches 引用
                foreach (var branch in step.Branches)
                {
                    if (idMap.TryGetValue(branch.NextStepId, out var newBranchNextId))
                        branch.NextStepId = newBranchNextId;
                }
            }

            return wf;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从文件导入工作流。</summary>
    public async Task<WorkflowDefinition?> ImportFromFileAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            return Import(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>导出工作流到文件。</summary>
    public async Task ExportToFileAsync(WorkflowDefinition workflow, string filePath, CancellationToken ct = default)
    {
        var json = Export(workflow);
        await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
    }
}
