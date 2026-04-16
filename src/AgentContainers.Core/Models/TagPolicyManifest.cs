namespace AgentContainers.Core.Models;

/// <summary>
/// Curated publish target that resolves one runtime/tool/agent loadout into
/// one or more public repository and tag views.
/// </summary>
public sealed class TagPolicyManifest : ManifestBase
{
    /// <summary>
    /// Base or combo runtime ID used as the starting image for this published image.
    /// </summary>
    public string Runtime { get; set; } = string.Empty;

    /// <summary>
    /// Agent overlays installed into the published image in the declared order.
    /// </summary>
    public List<string> Agents { get; set; } = [];

    /// <summary>
    /// Non-sidecar tool packs installed into the published image in the declared order.
    /// </summary>
    public List<string> ToolPacks { get; set; } = [];

    /// <summary>
    /// Human-facing release version used when rendering convenience tags.
    /// </summary>
    public string ReleaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// Repository/tag aliases that should point at this resolved image.
    /// </summary>
    public List<TagPublication> Publish { get; set; } = [];
}

public sealed class TagPublication
{
    public string Repository { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}
