using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using AgentContainers.Core.Models;

namespace AgentContainers.Core.Loading;

/// <summary>
/// Loads all manifest YAML files from a definitions root directory
/// and returns a populated ManifestCatalog.
/// </summary>
public sealed class ManifestLoader
{
    private readonly IDeserializer _deserializer;

    public ManifestLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads all manifests from the given definitions root directory.
    /// Expected subdirectories: common-tools, bases, combos, agents, tool-packs, compose, profiles, tag-policies
    /// </summary>
    public ManifestCatalog LoadAll(string definitionsRoot)
    {
        if (!Directory.Exists(definitionsRoot))
            throw new DirectoryNotFoundException($"Definitions root not found: {definitionsRoot}");

        var catalog = new ManifestCatalog();

        LoadManifests(catalog.CommonTools, Path.Combine(definitionsRoot, "common-tools"),
            yaml => Deserialize<CommonToolsManifest>(yaml));

        LoadManifests(catalog.Bases, Path.Combine(definitionsRoot, "bases"),
            yaml => Deserialize<BaseRuntimeManifest>(yaml));

        LoadManifests(catalog.Combos, Path.Combine(definitionsRoot, "combos"),
            yaml => Deserialize<ComboRuntimeManifest>(yaml));

        LoadManifests(catalog.Agents, Path.Combine(definitionsRoot, "agents"),
            yaml => Deserialize<AgentManifest>(yaml));

        LoadManifests(catalog.ToolPacks, Path.Combine(definitionsRoot, "tool-packs"),
            yaml => Deserialize<ToolPackManifest>(yaml));

        LoadManifests(catalog.ComposeStacks, Path.Combine(definitionsRoot, "compose"),
            yaml => Deserialize<ComposeStackManifest>(yaml));

        LoadManifests(catalog.Profiles, Path.Combine(definitionsRoot, "profiles"),
            yaml => Deserialize<ProfileManifest>(yaml));

        LoadManifests(catalog.TagPolicies, Path.Combine(definitionsRoot, "tag-policies"),
            yaml => Deserialize<TagPolicyManifest>(yaml));

        return catalog;
    }

    private static void LoadManifests<T>(Dictionary<string, T> target, string directory,
        Func<string, T> deserialize) where T : ManifestBase
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, "*.yaml").OrderBy(f => f, StringComparer.Ordinal))
        {
            var yaml = File.ReadAllText(file);
            try
            {
                var manifest = deserialize(yaml);
                if (target.ContainsKey(manifest.Id))
                    throw new InvalidOperationException(
                        $"Duplicate manifest ID '{manifest.Id}' in {directory}. " +
                        $"First loaded, then found again in {Path.GetFileName(file)}.");
                target[manifest.Id] = manifest;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize {file}: {ex.Message}", ex);
            }
        }
    }

    private T Deserialize<T>(string yaml) where T : ManifestBase
    {
        return _deserializer.Deserialize<T>(yaml)
               ?? throw new InvalidOperationException("Deserialized manifest was null.");
    }
}
