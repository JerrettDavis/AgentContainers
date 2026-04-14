using Scriban;
using Scriban.Runtime;
using AgentContainers.Core.Models;

namespace AgentContainers.Generator;

/// <summary>
/// Generates full docker-compose.yaml stacks from ComposeStackManifest definitions
/// using Scriban templates. Resolves agent/base/sidecar services into concrete
/// compose service blocks with environment, volumes, healthchecks, etc.
/// </summary>
internal static class ComposeStackGenerator
{
    /// <summary>
    /// Renders a complete docker-compose.yaml for the given stack using Scriban.
    /// </summary>
    internal static string GenerateStack(
        ComposeStackManifest stack,
        ManifestCatalog catalog,
        string templateText)
    {
        var services = ResolveServices(stack, catalog);

        var template = Template.Parse(templateText);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Scriban template parse errors: {string.Join("; ", template.Messages)}");

        var model = new ScriptObject();
        model["stack"] = BuildStackObject(stack);
        model["services"] = BuildServicesArray(services);
        model["networks"] = BuildNetworksArray(stack.Networks);
        model["volumes"] = BuildVolumesArray(stack.Volumes);

        var context = new TemplateContext();
        context.PushGlobal(model);

        return template.Render(context);
    }

    /// <summary>
    /// Resolves abstract service definitions into concrete service configurations
    /// by looking up agents, bases, and sidecars in the catalog.
    /// </summary>
    internal static List<ResolvedService> ResolveServices(
        ComposeStackManifest stack, ManifestCatalog catalog)
    {
        var resolved = new List<ResolvedService>();

        foreach (var svc in stack.Services)
        {
            var rs = new ResolvedService { Name = svc.Id };

            if (svc.Agent != null && catalog.Agents.TryGetValue(svc.Agent, out var agent))
            {
                ResolveAgentService(rs, svc, agent, catalog);
            }
            else if (svc.Type == "sidecar" || svc.Image != null)
            {
                ResolveSidecarService(rs, svc, catalog);
            }

            // Dependencies
            foreach (var dep in svc.DependsOn)
                rs.DependsOn.Add(new ResolvedDependency { Id = dep.Id, Condition = dep.Condition });

            // Profiles: explicit or derived from optional flag
            rs.Profiles.AddRange(svc.Profiles);
            if (svc.Optional && svc.Profiles.Count == 0)
                rs.Profiles.Add(svc.Id);

            // Stack-level env_file
            if (stack.EnvFile != null)
                rs.EnvFile = stack.EnvFile;

            resolved.Add(rs);
        }

        return resolved;
    }

    private static void ResolveAgentService(
        ResolvedService rs, ComposeService svc, AgentManifest agent, ManifestCatalog catalog)
    {
        var baseName = svc.Base ?? "node-bun";
        rs.Image = $"ghcr.io/agentcontainers/{baseName}-{agent.Id}:latest";

        foreach (var env in agent.Env)
        {
            if (env.Sensitive)
                rs.Environment.Add($"{env.Name}=${{{env.Name}:?{env.Name} is required}}");
            else if (env.Default != null)
                rs.Environment.Add($"{env.Name}={env.Default}");
        }

        // Wire tool-pack client env vars into agent services
        foreach (var packId in svc.Toolpacks)
        {
            if (catalog.ToolPacks.TryGetValue(packId, out var pack))
            {
                foreach (var env in pack.Env)
                {
                    var formatted = FormatEnvEntry(env);
                    if (formatted != null && !rs.Environment.Any(e => e.StartsWith(env.Name + "=")))
                        rs.Environment.Add(formatted);
                }
            }
        }

        foreach (var mount in agent.Mounts)
            rs.Volumes.Add($"{mount.Name}:{mount.ContainerPath}");

        rs.Ports.AddRange(agent.ComposeCapabilities.Ports);
        rs.Networks.AddRange(agent.ComposeCapabilities.Networks);

        if (agent.Healthcheck.Test.Count > 0)
        {
            rs.Healthcheck = new ResolvedHealthcheck
            {
                Test = agent.Healthcheck.Test,
                Interval = agent.Healthcheck.Interval,
                Timeout = agent.Healthcheck.Timeout,
                Retries = agent.Healthcheck.Retries,
                StartPeriod = agent.Healthcheck.StartPeriod
            };
        }

        rs.User = "1000:1000";
    }

    private static void ResolveSidecarService(ResolvedService rs, ComposeService svc, ManifestCatalog catalog)
    {
        rs.Image = svc.Image ?? $"ghcr.io/agentcontainers/{svc.Id}:latest";
        rs.Ports.AddRange(svc.Ports);
        rs.Networks.Add("agent-net");

        // Wire sidecar_env from the referenced tool-pack
        var packId = svc.Toolpack;
        if (packId != null && catalog.ToolPacks.TryGetValue(packId, out var pack))
        {
            foreach (var env in pack.SidecarEnv)
            {
                var formatted = FormatEnvEntry(env);
                if (formatted != null)
                    rs.Environment.Add(formatted);
            }
        }

        if (svc.Healthcheck is { Test.Count: > 0 })
        {
            rs.Healthcheck = new ResolvedHealthcheck
            {
                Test = svc.Healthcheck.Test,
                Interval = svc.Healthcheck.Interval,
                Timeout = svc.Healthcheck.Timeout,
                Retries = svc.Healthcheck.Retries,
                StartPeriod = svc.Healthcheck.StartPeriod
            };
        }

        rs.User = null; // sidecars run as their own user
    }

    /// <summary>
    /// Formats a single EnvVar into a compose environment entry string.
    /// Sensitive vars use ${VAR:-} (optional) or ${VAR:?error} (required) for host injection.
    /// Non-sensitive vars with defaults are inlined; others use ${VAR:-default} override pattern.
    /// </summary>
    private static string? FormatEnvEntry(EnvVar env)
    {
        if (env.Sensitive)
        {
            // Required sensitive: guard with error; optional sensitive: pass-through if set
            return env.Required
                ? $"{env.Name}=${{{env.Name}:?{env.Name} is required}}"
                : $"{env.Name}=${{{env.Name}:-}}";
        }

        if (env.Default != null)
            return $"{env.Name}=${{{env.Name}:-{env.Default}}}";

        // Non-sensitive, no default, not required — skip
        return null;
    }

    #region Scriban model builders

    private static ScriptObject BuildStackObject(ComposeStackManifest stack) => new()
    {
        ["id"] = stack.Id,
        ["display_name"] = stack.DisplayName,
        ["description"] = stack.Description
    };

    private static ScriptArray BuildServicesArray(List<ResolvedService> services)
    {
        var arr = new ScriptArray();
        foreach (var svc in services)
        {
            var obj = new ScriptObject
            {
                ["name"] = svc.Name,
                ["image"] = svc.Image,
                ["environment"] = new ScriptArray(svc.Environment),
                ["volumes"] = new ScriptArray(svc.Volumes),
                ["ports"] = new ScriptArray(svc.Ports),
                ["networks"] = new ScriptArray(svc.Networks),
                ["profiles"] = new ScriptArray(svc.Profiles),
                ["user"] = svc.User,
                ["env_file"] = svc.EnvFile
            };

            var depsArr = new ScriptArray();
            foreach (var dep in svc.DependsOn)
                depsArr.Add(new ScriptObject { ["id"] = dep.Id, ["condition"] = dep.Condition });
            obj["depends_on"] = depsArr;

            if (svc.Healthcheck != null)
            {
                var testFormatted = "[\"" + string.Join("\", \"", svc.Healthcheck.Test) + "\"]";
                obj["healthcheck"] = new ScriptObject
                {
                    ["test_formatted"] = testFormatted,
                    ["interval"] = svc.Healthcheck.Interval,
                    ["timeout"] = svc.Healthcheck.Timeout,
                    ["retries"] = svc.Healthcheck.Retries,
                    ["start_period"] = svc.Healthcheck.StartPeriod
                };
            }

            arr.Add(obj);
        }
        return arr;
    }

    private static ScriptArray BuildNetworksArray(List<NetworkDeclaration> networks)
    {
        var arr = new ScriptArray();
        foreach (var net in networks)
            arr.Add(new ScriptObject { ["name"] = net.Name, ["driver"] = net.Driver });
        return arr;
    }

    private static ScriptArray BuildVolumesArray(List<VolumeDeclaration> volumes)
    {
        var arr = new ScriptArray();
        foreach (var vol in volumes)
            arr.Add(new ScriptObject { ["name"] = vol.Name, ["description"] = vol.Description });
        return arr;
    }

    #endregion
}

#region Resolved models

internal sealed class ResolvedService
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<string> Environment { get; set; } = [];
    public List<string> Volumes { get; set; } = [];
    public List<string> Ports { get; set; } = [];
    public List<string> Networks { get; set; } = [];
    public List<ResolvedDependency> DependsOn { get; set; } = [];
    public ResolvedHealthcheck? Healthcheck { get; set; }
    public List<string> Profiles { get; set; } = [];
    public string? User { get; set; }
    public string? EnvFile { get; set; }
}

internal sealed class ResolvedDependency
{
    public string Id { get; set; } = string.Empty;
    public string Condition { get; set; } = "service_healthy";
}

internal sealed class ResolvedHealthcheck
{
    public List<string> Test { get; set; } = [];
    public string Interval { get; set; } = "30s";
    public string Timeout { get; set; } = "5s";
    public int Retries { get; set; } = 3;
    public string? StartPeriod { get; set; }
}

#endregion
