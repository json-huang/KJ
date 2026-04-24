using Microsoft.UI.Xaml.Media;

namespace KJ.Modules.Monitoring.Models;

public sealed class DeviceListItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty; // host:port
    public string StateText { get; init; } = string.Empty;
    public Brush? StateBrush { get; init; }
    public string LastConnectedText { get; init; } = string.Empty;
}

