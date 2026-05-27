using System.Text.Json.Serialization;

namespace KJ.Plugin.Host;

public sealed class PluginDescriptor
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("executablePath")]
    public string? ExecutablePath { get; set; }

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    [JsonPropertyName("grpcEndpoint")]
    public string GrpcEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; set; } = new();

    [JsonIgnore]
    public string SourcePath { get; set; } = string.Empty;
}
