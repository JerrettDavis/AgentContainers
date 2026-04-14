namespace AgentContainers.Core.Models;

/// <summary>
/// Compose stack definition: multi-service topology assembled from fragments.
/// Defined in definitions/compose/*.yaml
/// </summary>
public sealed class ComposeStackManifest : ManifestBase
{
    public List<ComposeService> Services { get; set; } = [];
    public List<NetworkDeclaration> Networks { get; set; } = [];
    public List<VolumeDeclaration> Volumes { get; set; } = [];
}

public sealed class ComposeService
{
    public required string Id { get; set; }
    public string? Agent { get; set; }
    public string? Base { get; set; }
    public string? Type { get; set; }
    public string? Toolpack { get; set; }
    public List<string> Toolpacks { get; set; } = [];
    public string? Image { get; set; }
    public bool Optional { get; set; }
    public List<string> Profiles { get; set; } = [];
    public List<string> Ports { get; set; } = [];
    public List<ServiceDependency> DependsOn { get; set; } = [];
    public HealthcheckConfig? Healthcheck { get; set; }
}

public sealed class ServiceDependency
{
    public required string Id { get; set; }
    public string Condition { get; set; } = "service_healthy";
}

public sealed class NetworkDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = "bridge";
}

public sealed class VolumeDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
