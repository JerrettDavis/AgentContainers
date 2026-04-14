using AgentContainers.Core.Models;

namespace AgentContainers.Core.Validation;

/// <summary>
/// Result of a single validation check.
/// </summary>
public sealed record ValidationResult(
    string ManifestType,
    string ManifestId,
    ValidationSeverity Severity,
    string Message);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Validates a ManifestCatalog for structural correctness, reference integrity,
/// and capability compatibility.
/// </summary>
public sealed class CatalogValidator
{
    public List<ValidationResult> Validate(ManifestCatalog catalog)
    {
        var results = new List<ValidationResult>();

        ValidateRequiredFields(catalog, results);
        ValidateBaseReferences(catalog, results);
        ValidateAgentCompatibility(catalog, results);
        ValidateToolPackCompatibility(catalog, results);
        ValidateComposeReferences(catalog, results);

        return results;
    }

    private static void ValidateRequiredFields(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (type, id, manifest) in catalog.All())
        {
            if (string.IsNullOrWhiteSpace(manifest.Id))
                results.Add(new(type, id, ValidationSeverity.Error, "Manifest ID is empty."));

            if (string.IsNullOrWhiteSpace(manifest.DisplayName))
                results.Add(new(type, id, ValidationSeverity.Error, "DisplayName is empty."));

            if (string.IsNullOrWhiteSpace(manifest.Version))
                results.Add(new(type, id, ValidationSeverity.Warning, "Version is empty, defaulting to v1."));
        }
    }

    private static void ValidateBaseReferences(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (comboId, combo) in catalog.Combos)
        {
            foreach (var baseRef in combo.Bases)
            {
                if (!catalog.Bases.ContainsKey(baseRef.Id))
                {
                    results.Add(new("combo", comboId, ValidationSeverity.Error,
                        $"References base '{baseRef.Id}' which does not exist in catalog."));
                }
            }
        }
    }

    private static void ValidateAgentCompatibility(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (agentId, agent) in catalog.Agents)
        {
            if (agent.Requires.Count == 0)
            {
                results.Add(new("agent", agentId, ValidationSeverity.Info,
                    "Agent has no runtime requirements; compatible with all bases."));
                continue;
            }

            // Check that at least one base or combo can satisfy requirements
            var satisfiedByAny = false;
            foreach (var (_, baseManifest) in catalog.Bases)
            {
                if (agent.Requires.All(req => CapabilitySatisfied(req, baseManifest.Provides)))
                {
                    satisfiedByAny = true;
                    break;
                }
            }

            if (!satisfiedByAny)
            {
                foreach (var (_, combo) in catalog.Combos)
                {
                    if (agent.Requires.All(req => CapabilitySatisfied(req, combo.Provides)))
                    {
                        satisfiedByAny = true;
                        break;
                    }
                }
            }

            if (!satisfiedByAny)
            {
                results.Add(new("agent", agentId, ValidationSeverity.Warning,
                    $"No base or combo in catalog satisfies requires: [{string.Join(", ", agent.Requires)}]. " +
                    "This may be expected if bases are loaded separately."));
            }
        }
    }

    private static void ValidateToolPackCompatibility(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (packId, pack) in catalog.ToolPacks)
        {
            foreach (var baseId in pack.CompatibleWith.Bases)
            {
                if (!catalog.Bases.ContainsKey(baseId) && !catalog.Combos.ContainsKey(baseId))
                {
                    results.Add(new("tool-pack", packId, ValidationSeverity.Warning,
                        $"Declares compatibility with '{baseId}' which is not in catalog."));
                }
            }
        }
    }

    private static void ValidateComposeReferences(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (stackId, stack) in catalog.ComposeStacks)
        {
            foreach (var svc in stack.Services)
            {
                if (svc.Agent != null && !catalog.Agents.ContainsKey(svc.Agent))
                {
                    results.Add(new("compose", stackId, ValidationSeverity.Warning,
                        $"Service '{svc.Id}' references agent '{svc.Agent}' not found in catalog."));
                }

                if (svc.Base != null && !catalog.Bases.ContainsKey(svc.Base) && !catalog.Combos.ContainsKey(svc.Base))
                {
                    results.Add(new("compose", stackId, ValidationSeverity.Warning,
                        $"Service '{svc.Id}' references base '{svc.Base}' not found in catalog."));
                }
            }
        }
    }

    /// <summary>
    /// Simple capability satisfaction: exact match or version-range match.
    /// For v1, we do prefix matching: "node>=18" is satisfied by "node>=22" or "node>=18".
    /// Full semver parsing is deferred.
    /// </summary>
    private static bool CapabilitySatisfied(string requirement, List<string> provides)
    {
        // Exact match
        if (provides.Contains(requirement))
            return true;

        // Simple prefix match: "node>=18" satisfied if any provides starts with "node"
        var baseToken = requirement.Split(">=")[0].Split("<=")[0];
        return provides.Any(p => p == baseToken || p.StartsWith(baseToken + ">="));
    }
}
