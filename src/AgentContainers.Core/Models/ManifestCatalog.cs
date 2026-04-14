namespace AgentContainers.Core.Models;

/// <summary>
/// Aggregated catalog of all loaded manifests, keyed by ID.
/// </summary>
public sealed class ManifestCatalog
{
    public Dictionary<string, CommonToolsManifest> CommonTools { get; } = [];
    public Dictionary<string, BaseRuntimeManifest> Bases { get; } = [];
    public Dictionary<string, ComboRuntimeManifest> Combos { get; } = [];
    public Dictionary<string, AgentManifest> Agents { get; } = [];
    public Dictionary<string, ToolPackManifest> ToolPacks { get; } = [];
    public Dictionary<string, ComposeStackManifest> ComposeStacks { get; } = [];
    public Dictionary<string, ProfileManifest> Profiles { get; } = [];

    public int TotalCount =>
        CommonTools.Count + Bases.Count + Combos.Count +
        Agents.Count + ToolPacks.Count + ComposeStacks.Count +
        Profiles.Count;

    public IEnumerable<(string Type, string Id, ManifestBase Manifest)> All()
    {
        foreach (var kv in CommonTools) yield return ("common-tools", kv.Key, kv.Value);
        foreach (var kv in Bases) yield return ("base", kv.Key, kv.Value);
        foreach (var kv in Combos) yield return ("combo", kv.Key, kv.Value);
        foreach (var kv in Agents) yield return ("agent", kv.Key, kv.Value);
        foreach (var kv in ToolPacks) yield return ("tool-pack", kv.Key, kv.Value);
        foreach (var kv in ComposeStacks) yield return ("compose", kv.Key, kv.Value);
        foreach (var kv in Profiles) yield return ("profile", kv.Key, kv.Value);
    }
}
