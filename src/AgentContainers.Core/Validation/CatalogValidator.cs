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

/// <summary>
/// Severity level attached to a catalog validation result.
/// </summary>
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
    /// <summary>
    /// Validates all loaded manifests and returns the accumulated findings.
    /// </summary>
    public List<ValidationResult> Validate(ManifestCatalog catalog)
    {
        var results = new List<ValidationResult>();

        ValidateRequiredFields(catalog, results);
        ValidateBaseReferences(catalog, results);
        ValidateAgentCompatibility(catalog, results);
        ValidateToolPackCompatibility(catalog, results);
        ValidateComposeReferences(catalog, results);
        ValidateTagPolicies(catalog, results);

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

    private static void ValidateTagPolicies(ManifestCatalog catalog, List<ValidationResult> results)
    {
        foreach (var (policyId, policy) in catalog.TagPolicies)
        {
            if (string.IsNullOrWhiteSpace(policy.Runtime))
            {
                results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                    "Tag policy runtime is empty."));
                continue;
            }

            List<string> runtimeProvides;
            string runtimeLabel;

            if (catalog.Bases.TryGetValue(policy.Runtime, out var baseRuntime))
            {
                runtimeProvides = baseRuntime.Provides;
                runtimeLabel = baseRuntime.DisplayName;
            }
            else if (catalog.Combos.TryGetValue(policy.Runtime, out var comboRuntime))
            {
                runtimeProvides = comboRuntime.Provides;
                runtimeLabel = comboRuntime.DisplayName;
            }
            else
            {
                results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                    $"References runtime '{policy.Runtime}' which does not exist in catalog."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(policy.ReleaseVersion) ||
                !System.Text.RegularExpressions.Regex.IsMatch(policy.ReleaseVersion, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
            {
                results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                    $"ReleaseVersion '{policy.ReleaseVersion}' is not a supported semantic version."));
            }

            if (policy.Publish.Count == 0)
            {
                results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                    "Tag policy must declare at least one publish target."));
            }

            foreach (var agentId in policy.Agents)
            {
                if (!catalog.Agents.TryGetValue(agentId, out var agent))
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"References agent '{agentId}' which does not exist in catalog."));
                    continue;
                }

                if (!agent.Requires.All(req => CapabilitySatisfied(req, runtimeProvides)))
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"Agent '{agentId}' is not compatible with runtime '{policy.Runtime}' ({runtimeLabel})."));
                }
            }

            foreach (var packId in policy.ToolPacks)
            {
                if (!catalog.ToolPacks.TryGetValue(packId, out var pack))
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"References tool pack '{packId}' which does not exist in catalog."));
                    continue;
                }

                if (pack.Sidecar?.Enabled == true)
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"Tool pack '{packId}' is sidecar-only and cannot be baked into a published image."));
                }

                if (!pack.CompatibleWith.Bases.Contains(policy.Runtime))
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"Tool pack '{packId}' is not compatible with runtime '{policy.Runtime}'."));
                }

                var incompatibleAgents = policy.Agents
                    .Where(agentId => pack.CompatibleWith.Agents.Count > 0 && !pack.CompatibleWith.Agents.Contains(agentId))
                    .ToList();
                if (incompatibleAgents.Count > 0)
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"Tool pack '{packId}' does not support agent(s): {string.Join(", ", incompatibleAgents)}."));
                }
            }

            var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var publication in policy.Publish)
            {
                if (string.IsNullOrWhiteSpace(publication.Repository))
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        "Publish target repository is empty."));
                    continue;
                }

                if (publication.Tags.Count == 0)
                {
                    results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                        $"Publish target '{publication.Repository}' must declare at least one tag template."));
                    continue;
                }

                foreach (var tag in publication.Tags)
                {
                    var composite = $"{publication.Repository}:{tag}";
                    if (!seenTags.Add(composite))
                    {
                        results.Add(new("tag-policy", policyId, ValidationSeverity.Error,
                            $"Duplicate publish tag template '{composite}' in tag policy."));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Simple capability satisfaction: exact match or version-range match.
    /// For v1, we do prefix matching: "node>=18" is satisfied by "node>=24" or "node>=18".
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
