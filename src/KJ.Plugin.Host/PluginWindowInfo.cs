using KJ.Plugin.Contracts;

namespace KJ.Plugin.Host;

public sealed record PluginWindowInfo(
    PluginDescriptor Descriptor,
    PluginManifest Manifest,
    string PageId,
    IntPtr Hwnd,
    string Title);
