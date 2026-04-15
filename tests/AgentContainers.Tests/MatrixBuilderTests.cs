using AgentContainers.Core.Loading;
using AgentContainers.Core.Matrix;
using AgentContainers.Core.Models;

namespace AgentContainers.Tests;

/// <summary>
/// Tests for MatrixBuilder — compatibility matrix and combination logic.
/// </summary>
public class MatrixBuilderTests
{
    private static ManifestCatalog LoadCatalog()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "definitions");
            if (Directory.Exists(candidate))
                return new ManifestLoader().LoadAll(candidate);
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not find definitions/ directory.");
    }

    [Fact]
    public void BuildAgentMatrix_ReturnsEntries()
    {
        var catalog = LoadCatalog();
        var matrix = MatrixBuilder.BuildAgentMatrix(catalog);

        Assert.NotEmpty(matrix);

        // Should have (bases + combos) × agents entries
        var expectedCount = (catalog.Bases.Count + catalog.Combos.Count) * catalog.Agents.Count;
        Assert.Equal(expectedCount, matrix.Count);
    }

    [Fact]
    public void BuildAgentMatrix_NodeBunIsCompatibleWithClaude()
    {
        var catalog = LoadCatalog();
        var matrix = MatrixBuilder.BuildAgentMatrix(catalog);

        var entry = matrix.FirstOrDefault(
            m => m.RuntimeId == "node-bun" && m.AgentId == "claude");
        Assert.NotNull(entry);
        Assert.True(entry.Compatible,
            "node-bun should be compatible with claude (provides node>=24, claude requires node>=18).");
    }

    [Fact]
    public void BuildAgentMatrix_DotnetIsNotCompatibleWithClaude()
    {
        var catalog = LoadCatalog();
        var matrix = MatrixBuilder.BuildAgentMatrix(catalog);

        var entry = matrix.FirstOrDefault(
            m => m.RuntimeId == "dotnet" && m.AgentId == "claude");
        Assert.NotNull(entry);
        Assert.False(entry.Compatible,
            "dotnet base should NOT be compatible with claude (no node capability).");
    }

    [Fact]
    public void BuildAgentMatrix_ComboNodePyDotnetCompatibleWithBothAgents()
    {
        var catalog = LoadCatalog();
        var matrix = MatrixBuilder.BuildAgentMatrix(catalog);

        var claudeEntry = matrix.FirstOrDefault(
            m => m.RuntimeId == "node-py-dotnet" && m.AgentId == "claude");
        Assert.NotNull(claudeEntry);
        Assert.True(claudeEntry.Compatible,
            "node-py-dotnet combo provides node, should be compatible with claude.");

        var openclawEntry = matrix.FirstOrDefault(
            m => m.RuntimeId == "node-py-dotnet" && m.AgentId == "openclaw");
        Assert.NotNull(openclawEntry);
        Assert.True(openclawEntry.Compatible,
            "node-py-dotnet combo provides node, should be compatible with openclaw.");
    }

    [Fact]
    public void BuildFullCombinations_OnlyReturnsCompatibleEntries()
    {
        var catalog = LoadCatalog();
        var combinations = MatrixBuilder.BuildFullCombinations(catalog);

        Assert.NotEmpty(combinations);
        Assert.All(combinations, c =>
        {
            var agent = catalog.Agents[c.AgentId];
            if (catalog.Bases.TryGetValue(c.RuntimeId, out var baseRuntime))
            {
                foreach (var req in agent.Requires)
                    Assert.True(MatrixBuilder.CapabilitySatisfied(req, baseRuntime.Provides),
                        $"Combination {c.RuntimeId}+{c.AgentId}: requirement '{req}' not met.");
            }
            else if (catalog.Combos.TryGetValue(c.RuntimeId, out var combo))
            {
                foreach (var req in agent.Requires)
                    Assert.True(MatrixBuilder.CapabilitySatisfied(req, combo.Provides),
                        $"Combination {c.RuntimeId}+{c.AgentId}: requirement '{req}' not met.");
            }
        });
    }

    [Fact]
    public void BuildFullCombinations_IncludesToolPacks()
    {
        var catalog = LoadCatalog();
        var combinations = MatrixBuilder.BuildFullCombinations(catalog);

        // At least some combinations should include headroom tool-pack
        var withToolPacks = combinations.Where(c => c.ToolPacks.Count > 0).ToList();
        Assert.NotEmpty(withToolPacks);
        Assert.True(withToolPacks.Any(c => c.ToolPacks.Contains("headroom")),
            "At least one combination should include headroom tool-pack.");
    }

    [Fact]
    public void GetCompatibleToolPacks_HeadroomCompatibleWithNodeBunClaude()
    {
        var catalog = LoadCatalog();
        var packs = MatrixBuilder.GetCompatibleToolPacks(catalog, "node-bun", "claude");
        Assert.Contains("headroom", packs);
    }

    [Fact]
    public void CapabilitySatisfied_ExactMatch()
    {
        Assert.True(MatrixBuilder.CapabilitySatisfied("node", ["node", "npm"]));
    }

    [Fact]
    public void CapabilitySatisfied_VersionRangeSatisfied()
    {
        Assert.True(MatrixBuilder.CapabilitySatisfied("node>=18", ["node>=24", "npm"]));
    }

    [Fact]
    public void CapabilitySatisfied_NoMatchReturnsFalse()
    {
        Assert.False(MatrixBuilder.CapabilitySatisfied("node>=18", ["python", "pip"]));
    }

    [Fact]
    public void CapabilitySatisfied_BaseTokenMatch()
    {
        Assert.True(MatrixBuilder.CapabilitySatisfied("node", ["node>=24"]));
    }
}
