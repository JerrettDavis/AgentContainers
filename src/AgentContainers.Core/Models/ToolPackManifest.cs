namespace AgentContainers.Core.Models;

/// <summary>
/// Layer 5: Optional, additive tool pack overlay.
/// Defined in definitions/tool-packs/*.yaml
/// </summary>
public sealed class ToolPackManifest : ManifestBase
{
    public CompatibilityFilter CompatibleWith { get; set; } = new();
    public List<string> Conflicts { get; set; } = [];
    public AgentInstallBlock Install { get; set; } = new();

    /// <summary>Client-side env vars injected into agent containers that use this pack.</summary>
    public List<EnvVar> Env { get; set; } = [];

    /// <summary>Env vars injected into the sidecar container itself.</summary>
    public List<EnvVar> SidecarEnv { get; set; } = [];

    public SidecarConfig? Sidecar { get; set; }
    public ValidationBlock Validation { get; set; } = new();
    public ComposeCapabilities ComposeCapabilities { get; set; } = new();
}

public sealed class CompatibilityFilter
{
    public List<string> Bases { get; set; } = [];
    public List<string> Agents { get; set; } = [];
}

public sealed class SidecarConfig
{
    public bool Enabled { get; set; }
    public string ServiceDefinition { get; set; } = string.Empty;
    public bool DependsOnAgent { get; set; }
}
