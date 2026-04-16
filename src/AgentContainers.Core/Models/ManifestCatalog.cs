namespace AgentContainers.Core.Models;

/// <summary>
/// Aggregated catalog of all loaded manifests, keyed by ID.
/// </summary>
public sealed class ManifestCatalog
{
    /// <summary>Common-tools manifests keyed by ID.</summary>
    public Dictionary<string, CommonToolsManifest> CommonTools { get; } = [];

    /// <summary>Base runtime manifests keyed by ID.</summary>
    public Dictionary<string, BaseRuntimeManifest> Bases { get; } = [];

    /// <summary>Combo runtime manifests keyed by ID.</summary>
    public Dictionary<string, ComboRuntimeManifest> Combos { get; } = [];

    /// <summary>Agent manifests keyed by ID.</summary>
    public Dictionary<string, AgentManifest> Agents { get; } = [];

    /// <summary>Tool-pack manifests keyed by ID.</summary>
    public Dictionary<string, ToolPackManifest> ToolPacks { get; } = [];

    /// <summary>Compose stack manifests keyed by ID.</summary>
    public Dictionary<string, ComposeStackManifest> ComposeStacks { get; } = [];

    /// <summary>Profile manifests keyed by ID.</summary>
    public Dictionary<string, ProfileManifest> Profiles { get; } = [];

    /// <summary>Curated publish target manifests keyed by ID.</summary>
    public Dictionary<string, TagPolicyManifest> TagPolicies { get; } = [];

    /// <summary>Total number of loaded manifests across all categories.</summary>
    public int TotalCount =>
        CommonTools.Count + Bases.Count + Combos.Count +
        Agents.Count + ToolPacks.Count + ComposeStacks.Count +
        Profiles.Count + TagPolicies.Count;

    /// <summary>
    /// Enumerates all manifests as a normalized sequence of <c>(type, id, manifest)</c> tuples.
    /// </summary>
    public IEnumerable<(string Type, string Id, ManifestBase Manifest)> All()
    {
        foreach (var kv in CommonTools) yield return ("common-tools", kv.Key, kv.Value);
        foreach (var kv in Bases) yield return ("base", kv.Key, kv.Value);
        foreach (var kv in Combos) yield return ("combo", kv.Key, kv.Value);
        foreach (var kv in Agents) yield return ("agent", kv.Key, kv.Value);
        foreach (var kv in ToolPacks) yield return ("tool-pack", kv.Key, kv.Value);
        foreach (var kv in ComposeStacks) yield return ("compose", kv.Key, kv.Value);
        foreach (var kv in Profiles) yield return ("profile", kv.Key, kv.Value);
        foreach (var kv in TagPolicies) yield return ("tag-policy", kv.Key, kv.Value);
    }
}
