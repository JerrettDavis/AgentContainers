using AgentContainers.Core.Loading;
using AgentContainers.Core.Models;

namespace AgentContainers.Tests;

/// <summary>
/// Tests that the ManifestLoader can load the real definitions/ directory
/// and produce a valid ManifestCatalog.
/// </summary>
public class ManifestLoaderTests
{
    private static string GetDefinitionsRoot()
    {
        // Walk up from the test bin directory to find the repo root
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "definitions");
            if (Directory.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException(
            "Could not find definitions/ directory from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void LoadAll_ReturnsNonEmptyCatalog()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.TotalCount > 0, "Catalog should have at least one manifest.");
    }

    [Fact]
    public void LoadAll_LoadsBases()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.Bases.Count >= 3, "Should load at least 3 bases (dotnet, node-bun, python).");
        Assert.Contains("dotnet", catalog.Bases.Keys);
        Assert.Contains("node-bun", catalog.Bases.Keys);
        Assert.Contains("python", catalog.Bases.Keys);
    }

    [Fact]
    public void LoadAll_LoadsAgents()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.Agents.Count >= 2, "Should load at least 2 agents.");
        Assert.Contains("claude", catalog.Agents.Keys);
        Assert.Contains("openclaw", catalog.Agents.Keys);
    }

    [Fact]
    public void LoadAll_LoadsCombos()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.Combos.Count >= 1, "Should load at least 1 combo.");
        Assert.Contains("node-py-dotnet", catalog.Combos.Keys);
    }

    [Fact]
    public void LoadAll_LoadsToolPacks()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.ToolPacks.Count >= 1, "Should load at least 1 tool-pack.");
        Assert.Contains("headroom", catalog.ToolPacks.Keys);
    }

    [Fact]
    public void LoadAll_LoadsTagPolicies()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        Assert.True(catalog.TagPolicies.Count >= 3, "Should load curated publish targets.");
        Assert.Contains("dotnet-claude", catalog.TagPolicies.Keys);
        Assert.Contains("polyglot-menagerie", catalog.TagPolicies.Keys);
    }

    [Fact]
    public void LoadAll_ManifestsHaveRequiredFields()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        foreach (var (type, id, manifest) in catalog.All())
        {
            Assert.False(string.IsNullOrWhiteSpace(manifest.Id),
                $"{type}:{id} should have a non-empty Id.");
            Assert.False(string.IsNullOrWhiteSpace(manifest.DisplayName),
                $"{type}:{id} should have a non-empty DisplayName.");
        }
    }

    [Fact]
    public void LoadAll_BasesProvidesArePopulated()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        foreach (var (id, baseManifest) in catalog.Bases)
        {
            Assert.True(baseManifest.Provides.Count > 0,
                $"Base '{id}' should provide at least one capability.");
        }
    }

    [Fact]
    public void LoadAll_AgentRequiresAreParsed()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());

        var claude = catalog.Agents["claude"];
        Assert.Contains("node>=18", claude.Requires);
    }

    [Fact]
    public void LoadAll_ThrowsForMissingDirectory()
    {
        var loader = new ManifestLoader();
        Assert.Throws<DirectoryNotFoundException>(
            () => loader.LoadAll("/nonexistent/path/definitions"));
    }
}
