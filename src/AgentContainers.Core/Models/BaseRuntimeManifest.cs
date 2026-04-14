namespace AgentContainers.Core.Models;

/// <summary>
/// Layer 2: Single language runtime base image.
/// Defined in definitions/bases/*.yaml
/// </summary>
public sealed class BaseRuntimeManifest : ManifestBase
{
    public string Family { get; set; } = string.Empty;
    public FromImage From { get; set; } = new();
    public string CommonTools { get; set; } = "default";
    public List<string> Provides { get; set; } = [];
    public List<string> Platforms { get; set; } = ["linux/amd64"];
    public InstallBlock Install { get; set; } = new();
    public List<EnvVar> Env { get; set; } = [];
    public List<MountDeclaration> Mounts { get; set; } = [];
    public UserConfig User { get; set; } = new();
    public string Shell { get; set; } = "bash";
    public HealthcheckConfig Healthcheck { get; set; } = new();
    public ValidationBlock Validation { get; set; } = new();
    public ResourceHints ResourceHints { get; set; } = new();
}

public sealed class FromImage
{
    public string Image { get; set; } = string.Empty;
    public string? Digest { get; set; }
}

public sealed class InstallBlock
{
    public List<InstallStep> Steps { get; set; } = [];
}

public sealed class InstallStep
{
    public string Description { get; set; } = string.Empty;
    public string Run { get; set; } = string.Empty;
}

public sealed class EnvVar
{
    public required string Name { get; set; }
    public string? Default { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Sensitive { get; set; }
    public string? InjectFrom { get; set; }
}

public sealed class MountDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string ContainerPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? SuggestedHostPath { get; set; }
}

public sealed class UserConfig
{
    public string Name { get; set; } = "dev";
    public int Uid { get; set; } = 1000;
    public int Gid { get; set; } = 1000;
    public bool Sudo { get; set; } = true;
}

public sealed class HealthcheckConfig
{
    public List<string> Test { get; set; } = [];
    public string Interval { get; set; } = "30s";
    public string Timeout { get; set; } = "5s";
    public int Retries { get; set; } = 3;
    public string? StartPeriod { get; set; }
}

public sealed class ResourceHints
{
    public string ImageSizeClass { get; set; } = "medium";
    public int MemoryMinimumMb { get; set; } = 512;
}
