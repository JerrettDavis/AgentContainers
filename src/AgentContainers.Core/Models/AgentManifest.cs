namespace AgentContainers.Core.Models;

/// <summary>
/// Layer 4: Agent provider overlay (installed on compatible base/combo).
/// Defined in definitions/agents/*.yaml
/// </summary>
public sealed class AgentManifest : ManifestBase
{
    public List<string> Requires { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
    public AgentInstallBlock Install { get; set; } = new();
    public List<EnvVar> Env { get; set; } = [];
    public List<MountDeclaration> Mounts { get; set; } = [];
    public HealthcheckConfig Healthcheck { get; set; } = new();
    public ValidationBlock Validation { get; set; } = new();
    public List<ShellHelper> ShellHelpers { get; set; } = [];
    public List<string> KnownCaveats { get; set; } = [];
    public List<string> PrivilegedModes { get; set; } = [];
    public ComposeCapabilities ComposeCapabilities { get; set; } = new();
}

public sealed class AgentInstallBlock
{
    public string Method { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public string Version { get; set; } = "latest";
    public List<PostInstallStep> PostInstall { get; set; } = [];
}

public sealed class ShellHelper
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ComposeCapabilities
{
    public string ServiceNameDefault { get; set; } = string.Empty;
    public List<string> Ports { get; set; } = [];
    public List<string> Networks { get; set; } = ["agent-net"];
}
