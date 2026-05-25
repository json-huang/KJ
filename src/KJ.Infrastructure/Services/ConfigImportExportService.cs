using System.Text.Json;
using KJ.Domain;
using KJ.Domain.Services;

namespace KJ.Infrastructure.Services;

/// <summary>
/// 配置导入导出服务。支持设备和标签配置的 JSON 导入导出。
/// </summary>
public sealed class ConfigImportExportService
{
    private readonly IDeviceManager _deviceManager;
    private readonly TagManager _tagManager;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ConfigImportExportService(IDeviceManager deviceManager, TagManager tagManager)
    {
        _deviceManager = deviceManager;
        _tagManager = tagManager;
    }

    /// <summary>导出所有配置（设备+标签）为 JSON。</summary>
    public string ExportAll()
    {
        var config = new ConfigExport
        {
            ExportedAt = DateTimeOffset.Now,
            Devices = _deviceManager.ListDevices().Select(d => new DeviceExport
            {
                DeviceId = d.DeviceId,
                DisplayName = d.DisplayName,
                DriverType = d.DriverType,
                Host = d.Host,
                Port = d.Port,
                Extra = d.Extra,
            }).ToList(),
            Tags = _tagManager.GetAllTags().Select(t => new TagExport
            {
                TagId = t.TagId,
                TagKey = t.TagKey,
                DeviceId = t.DeviceId,
                Address = t.Address,
                ValueType = t.ValueType.ToString(),
                PollIntervalMs = t.PollIntervalMs,
            }).ToList(),
        };

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    /// <summary>导出为 JSON 字节数组。</summary>
    public byte[] ExportToBytes() => System.Text.Encoding.UTF8.GetBytes(ExportAll());

    /// <summary>导入配置。返回导入结果。</summary>
    public ConfigImportResult Import(string json, bool overwrite = false)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ConfigExport>(json, JsonOptions);
            if (config is null)
                return new ConfigImportResult(false, "Invalid JSON format");

            var devicesAdded = 0;
            var tagsAdded = 0;
            var errors = new List<string>();

            // 导入设备
            foreach (var dev in config.Devices)
            {
                try
                {
                    var existing = _deviceManager.GetDevice(dev.DeviceId);
                    if (existing is not null && !overwrite)
                    {
                        errors.Add($"Device '{dev.DeviceId}' already exists, skipped");
                        continue;
                    }

                    var descriptor = new DeviceDescriptor(
                        dev.DeviceId, dev.DisplayName, dev.DriverType,
                        Host: dev.Host, Port: dev.Port, Extra: dev.Extra);

                    if (existing is not null)
                        _deviceManager.RemoveDevice(dev.DeviceId);

                    _deviceManager.AddDevice(descriptor);
                    devicesAdded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Device '{dev.DeviceId}': {ex.Message}");
                }
            }

            // 导入标签
            foreach (var tag in config.Tags)
            {
                try
                {
                    if (!Enum.TryParse<TagValueType>(tag.ValueType, true, out var valueType))
                        valueType = TagValueType.Int32;

                    var existing = _tagManager.GetTag(tag.TagId);
                    if (existing is not null && !overwrite)
                    {
                        errors.Add($"Tag '{tag.TagKey}' already exists, skipped");
                        continue;
                    }

                    if (existing is not null)
                        _tagManager.RemoveTag(tag.TagId);

                    _tagManager.AddTag(new TagConfig(
                        tag.TagId, tag.TagKey, tag.DeviceId, tag.Address,
                        valueType, tag.PollIntervalMs));
                    tagsAdded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Tag '{tag.TagKey}': {ex.Message}");
                }
            }

            return new ConfigImportResult(true, null, devicesAdded, tagsAdded, errors);
        }
        catch (Exception ex)
        {
            return new ConfigImportResult(false, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>从文件导入。</summary>
    public async Task<ConfigImportResult> ImportFromFileAsync(string filePath, bool overwrite = false, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        return Import(json, overwrite);
    }
}

public sealed class ConfigExport
{
    public DateTimeOffset ExportedAt { get; set; }
    public List<DeviceExport> Devices { get; set; } = new();
    public List<TagExport> Tags { get; set; } = new();
}

public sealed class DeviceExport
{
    public string DeviceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DriverType { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string? Extra { get; set; }
}

public sealed class TagExport
{
    public Guid TagId { get; set; }
    public string TagKey { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Address { get; set; } = "";
    public string ValueType { get; set; } = "Int32";
    public int PollIntervalMs { get; set; }
}

public sealed record ConfigImportResult(
    bool Success,
    string? Error = null,
    int DevicesAdded = 0,
    int TagsAdded = 0,
    IReadOnlyList<string>? Warnings = null);
