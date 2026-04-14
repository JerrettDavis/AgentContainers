namespace AgentContainers.Core.Models;

/// <summary>
/// Common fields shared by all manifest types.
/// </summary>
public abstract class ManifestBase
{
    /// <summary>Stable manifest identifier used in file names, tags, and references.</summary>
    public required string Id { get; set; }

    /// <summary>Human-readable name shown in generated docs, catalogs, and logs.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Schema or manifest version identifier.</summary>
    public string Version { get; set; } = "v1";

    /// <summary>Human-readable manifest description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Maintainers responsible for the manifest.</summary>
    public List<Maintainer> Maintainers { get; set; } = [];

    /// <summary>Additional OCI-style or internal labels associated with the manifest.</summary>
    public Dictionary<string, string> Labels { get; set; } = [];
}

/// <summary>
/// Repository or image maintainer metadata.
/// </summary>
public sealed class Maintainer
{
    public string Name { get; set; } = string.Empty;
    public string Github { get; set; } = string.Empty;
}
