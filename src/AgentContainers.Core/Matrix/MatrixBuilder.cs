using AgentContainers.Core.Models;

namespace AgentContainers.Core.Matrix;

/// <summary>
/// A single entry in the compatibility matrix: one runtime × one agent combination.
/// </summary>
public sealed record MatrixEntry(
    string RuntimeId,
    string RuntimeType,
    string AgentId,
    bool Compatible);

/// <summary>
/// Builds the full compatibility matrix from a ManifestCatalog.
/// Determines which runtime (base or combo) can host which agent,
/// and which tool-packs are compatible with each pairing.
/// </summary>
public static class MatrixBuilder
{
    /// <summary>
    /// Computes the full agent-to-runtime compatibility matrix.
    /// </summary>
    public static List<MatrixEntry> BuildAgentMatrix(ManifestCatalog catalog)
    {
        var entries = new List<MatrixEntry>();
        var agentIds = catalog.Agents.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        var runtimes = catalog.Bases.Values
            .Select(b => (b.Id, Type: "base", b.Provides))
            .Concat(catalog.Combos.Values.Select(c => (c.Id, Type: "combo", c.Provides)))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var (runtimeId, runtimeType, provides) in runtimes)
        {
            foreach (var agentId in agentIds)
            {
                var agent = catalog.Agents[agentId];
                var compatible = agent.Requires.Count == 0 ||
                    agent.Requires.All(req => CapabilitySatisfied(req, provides));
                entries.Add(new MatrixEntry(runtimeId, runtimeType, agentId, compatible));
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns the subset of tool-packs compatible with a given runtime + agent pair.
    /// </summary>
    public static List<string> GetCompatibleToolPacks(
        ManifestCatalog catalog, string runtimeId, string agentId)
    {
        var result = new List<string>();
        foreach (var (packId, pack) in catalog.ToolPacks)
        {
            var baseOk = pack.CompatibleWith.Bases.Count == 0 ||
                pack.CompatibleWith.Bases.Contains(runtimeId);
            var agentOk = pack.CompatibleWith.Agents.Count == 0 ||
                pack.CompatibleWith.Agents.Contains(agentId);

            if (baseOk && agentOk)
                result.Add(packId);
        }
        return result;
    }

    /// <summary>
    /// Returns all valid (runtime, agent, tool-packs[]) combinations.
    /// </summary>
    public static List<MatrixCombination> BuildFullCombinations(ManifestCatalog catalog)
    {
        var agentEntries = BuildAgentMatrix(catalog);
        var result = new List<MatrixCombination>();

        foreach (var entry in agentEntries.Where(e => e.Compatible))
        {
            var toolPacks = GetCompatibleToolPacks(catalog, entry.RuntimeId, entry.AgentId);
            result.Add(new MatrixCombination(
                entry.RuntimeId, entry.RuntimeType, entry.AgentId, toolPacks));
        }

        return result;
    }

    /// <summary>
    /// Simple capability matching — same logic as CatalogValidator.
    /// Exact match, or prefix-based version match.
    /// </summary>
    internal static bool CapabilitySatisfied(string requirement, List<string> provides)
    {
        if (provides.Contains(requirement))
            return true;

        var baseToken = requirement.Split(">=")[0].Split("<=")[0];
        return provides.Any(p => p == baseToken || p.StartsWith(baseToken + ">="));
    }
}

/// <summary>
/// A fully resolved combination: runtime + agent + compatible tool-packs.
/// </summary>
public sealed record MatrixCombination(
    string RuntimeId,
    string RuntimeType,
    string AgentId,
    List<string> ToolPacks);
