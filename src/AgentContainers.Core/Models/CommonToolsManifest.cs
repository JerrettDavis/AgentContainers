namespace AgentContainers.Core.Models;

/// <summary>
/// Layer 1: Universal common tools installed on every image.
/// Defined in definitions/common-tools/*.yaml
/// </summary>
public sealed class CommonToolsManifest : ManifestBase
{
    public PackageSet Packages { get; set; } = new();
    public List<PostInstallStep> PostInstall { get; set; } = [];
    public ValidationBlock Validation { get; set; } = new();
}

public sealed class PackageSet
{
    public List<string> Apt { get; set; } = [];
}

public sealed class PostInstallStep
{
    public string Command { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ValidationBlock
{
    public List<string> Commands { get; set; } = [];
}
