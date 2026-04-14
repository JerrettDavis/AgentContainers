namespace AgentContainers.Core.Models;

/// <summary>
/// Profile manifest: named sets of services/agents to activate together.
/// Defined in definitions/profiles/*.yaml
/// </summary>
public sealed class ProfileManifest : ManifestBase
{
    public List<string> IncludeServices { get; set; } = [];
    public List<string> IncludeAgents { get; set; } = [];
    public List<string> IncludeToolPacks { get; set; } = [];
}
