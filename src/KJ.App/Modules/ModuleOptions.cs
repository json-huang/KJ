namespace KJ.App.Modules;

public sealed class ModuleOptions
{
    public List<ModuleDescriptor> Items { get; init; } = [];
}

public sealed class ModuleDescriptor
{
    public string Id { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Assembly-qualified type name for a Prism <see cref="Prism.Modularity.IModule"/>.
    /// Example: "KJ.Modules.Auth.AuthModule, KJ.Modules.Auth"
    /// </summary>
    public string ModuleType { get; init; } = string.Empty;
}

