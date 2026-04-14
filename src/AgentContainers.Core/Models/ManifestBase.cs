namespace AgentContainers.Core.Models;

/// <summary>
/// Common fields shared by all manifest types.
/// </summary>
public abstract class ManifestBase
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = string.Empty;
    public List<Maintainer> Maintainers { get; set; } = [];
    public Dictionary<string, string> Labels { get; set; } = [];
}

public sealed class Maintainer
{
    public string Name { get; set; } = string.Empty;
    public string Github { get; set; } = string.Empty;
}
