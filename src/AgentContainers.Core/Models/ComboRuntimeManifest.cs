namespace AgentContainers.Core.Models;

/// <summary>
/// Layer 3: Multi-language combo runtime (ordered union of bases).
/// Defined in definitions/combos/*.yaml
/// </summary>
public sealed class ComboRuntimeManifest : ManifestBase
{
    public List<BaseReference> Bases { get; set; } = [];
    public List<string> Provides { get; set; } = [];
    public PackageSet CrossRuntimeUtilities { get; set; } = new();
    public ValidationBlock Validation { get; set; } = new();
    public ResourceHints ResourceHints { get; set; } = new();
}

public sealed class BaseReference
{
    public required string Id { get; set; }
    public int Order { get; set; }
}
