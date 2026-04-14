using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContainers.Core.Hashing;
using AgentContainers.Core.Loading;
using AgentContainers.Core.Matrix;
using AgentContainers.Core.Validation;

namespace AgentContainers.Generator;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "generate";
        var jsonOutput = args.Contains("--json");
        var repoRoot = FindRepoRoot();

        if (repoRoot == null)
        {
            Console.Error.WriteLine("Error: Could not locate repository root (looking for definitions/ directory).");
            return 1;
        }

        if (!jsonOutput)
        {
            Console.WriteLine($"AgentContainers Generator v0.1.0");
            Console.WriteLine($"Repository root: {repoRoot}");
            Console.WriteLine();
        }

        return command switch
        {
            "generate" => await RunGenerate(repoRoot),
            "validate" => RunValidate(repoRoot),
            "list-matrix" => RunListMatrix(repoRoot, jsonOutput),
            "build-matrix" => RunBuildMatrix(repoRoot),
            _ => PrintUsage()
        };
    }

    private static async Task<int> RunGenerate(string repoRoot)
    {
        Console.WriteLine("=== Loading Manifests ===");
        var loader = new ManifestLoader();
        var definitionsRoot = Path.Combine(repoRoot, "definitions");
        var catalog = loader.LoadAll(definitionsRoot);
        PrintCatalogSummary(catalog);

        Console.WriteLine();
        Console.WriteLine("=== Computing Manifest Hash ===");
        var manifestHash = ContentHasher.ComputeManifestHash(definitionsRoot);
        Console.WriteLine($"  Manifest hash: {manifestHash}");

        Console.WriteLine();
        Console.WriteLine("=== Validating Catalog ===");
        var validator = new CatalogValidator();
        var results = validator.Validate(catalog);
        PrintValidationResults(results);

        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        if (hasErrors)
        {
            Console.Error.WriteLine("Validation errors found. Aborting generation.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("=== Loading Schemas ===");
        var schemaValidator = new SchemaValidator();
        await schemaValidator.LoadSchemasAsync(Path.Combine(repoRoot, "schemas"));
        Console.WriteLine($"Loaded {schemaValidator.LoadedSchemas.Count} schema(s): {string.Join(", ", schemaValidator.LoadedSchemas)}");

        Console.WriteLine();
        Console.WriteLine("=== Generating Artifacts ===");
        var generatedRoot = Path.Combine(repoRoot, "generated");
        var templateRoot = Path.Combine(repoRoot, "templates");
        var report = GenerateArtifacts(catalog, generatedRoot, templateRoot, manifestHash);

        var reportPath = Path.Combine(generatedRoot, "generation-report.json");
        var reportJson = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(reportPath, reportJson);
        Console.WriteLine($"Wrote generation report: {reportPath}");

        Console.WriteLine();
        Console.WriteLine($"Generation complete. {report.Artifacts.Count} artifact(s) written.");
        Console.WriteLine($"  Manifest hash: {manifestHash}");
        return 0;
    }

    private static int RunValidate(string repoRoot)
    {
        Console.WriteLine("=== Loading Manifests ===");
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(Path.Combine(repoRoot, "definitions"));
        PrintCatalogSummary(catalog);

        Console.WriteLine();
        Console.WriteLine("=== Validating Catalog ===");
        var validator = new CatalogValidator();
        var results = validator.Validate(catalog);
        PrintValidationResults(results);

        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return hasErrors ? 1 : 0;
    }

    private static int RunListMatrix(string repoRoot, bool jsonOutput)
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(Path.Combine(repoRoot, "definitions"));

        var matrix = MatrixBuilder.BuildAgentMatrix(catalog);
        var combinations = MatrixBuilder.BuildFullCombinations(catalog);

        if (jsonOutput)
        {
            var output = new
            {
                matrix = matrix.Select(e => new
                {
                    runtime_id = e.RuntimeId,
                    runtime_type = e.RuntimeType,
                    agent_id = e.AgentId,
                    compatible = e.Compatible
                }),
                combinations = combinations.Select(c => new
                {
                    runtime_id = c.RuntimeId,
                    runtime_type = c.RuntimeType,
                    agent_id = c.AgentId,
                    tool_packs = c.ToolPacks
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
            return 0;
        }

        Console.WriteLine("=== Loading Manifests ===");
        PrintCatalogSummary(catalog);

        Console.WriteLine();
        Console.WriteLine("=== Compatibility Matrix ===");
        Console.WriteLine();

        var agentIds = catalog.Agents.Keys.OrderBy(k => k).ToList();
        Console.Write("Base/Combo".PadRight(25));
        foreach (var agentId in agentIds)
            Console.Write(agentId.PadRight(15));
        Console.WriteLine();
        Console.WriteLine(new string('-', 25 + agentIds.Count * 15));

        var runtimeIds = matrix.Select(m => m.RuntimeId).Distinct().ToList();
        foreach (var runtimeId in runtimeIds)
        {
            Console.Write(runtimeId.PadRight(25));
            foreach (var agentId in agentIds)
            {
                var entry = matrix.First(m => m.RuntimeId == runtimeId && m.AgentId == agentId);
                Console.Write((entry.Compatible ? "✓" : "—").PadRight(15));
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("=== Valid Combinations ===");
        Console.WriteLine();
        foreach (var combo in combinations)
        {
            var packs = combo.ToolPacks.Count > 0
                ? $" + [{string.Join(", ", combo.ToolPacks)}]"
                : "";
            Console.WriteLine($"  {combo.RuntimeId} ({combo.RuntimeType}) + {combo.AgentId}{packs}");
        }
        Console.WriteLine($"\n  Total: {combinations.Count} valid combination(s)");

        return 0;
    }

    private static int RunBuildMatrix(string repoRoot)
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(Path.Combine(repoRoot, "definitions"));
        var manifestHash = ContentHasher.ComputeManifestHash(Path.Combine(repoRoot, "definitions"));

        var include = new List<object>();

        foreach (var (id, b) in catalog.Bases)
        {
            var platforms = b.Platforms.Count > 0
                ? string.Join(",", b.Platforms)
                : "linux/amd64";

            include.Add(new
            {
                id,
                type = "base",
                display_name = b.DisplayName,
                context = $"generated/docker/bases/{id}",
                dockerfile = "Dockerfile",
                image_name = $"ghcr.io/${{{{ github.repository_owner }}}}/{id}",
                platforms,
                family = b.Family,
                manifest_hash = manifestHash
            });
        }

        foreach (var (id, c) in catalog.Combos)
        {
            // Derive platforms from the primary (first-ordered) base
            var primaryBase = c.Bases.OrderBy(b => b.Order).FirstOrDefault();
            var platforms = "linux/amd64";
            if (primaryBase != null && catalog.Bases.TryGetValue(primaryBase.Id, out var pb))
            {
                platforms = pb.Platforms.Count > 0
                    ? string.Join(",", pb.Platforms)
                    : "linux/amd64";
            }

            include.Add(new
            {
                id,
                type = "combo",
                display_name = c.DisplayName,
                context = $"generated/docker/combos/{id}",
                dockerfile = "Dockerfile",
                image_name = $"ghcr.io/${{{{ github.repository_owner }}}}/{id}",
                platforms,
                family = string.Join("+", c.Bases.OrderBy(b => b.Order).Select(b => b.Id)),
                manifest_hash = manifestHash
            });
        }

        var matrix = new { include };
        Console.Write(JsonSerializer.Serialize(matrix, JsonOptions));
        return 0;
    }

    private static GenerationReport GenerateArtifacts(
        AgentContainers.Core.Models.ManifestCatalog catalog,
        string generatedRoot,
        string templateRoot,
        string manifestHash)
    {
        var report = new GenerationReport
        {
            ManifestHash = manifestHash,
            GeneratorVersion = "0.1.0"
        };

        // Generate image catalog summary
        var dockerRoot = Path.Combine(generatedRoot, "docker");
        Directory.CreateDirectory(dockerRoot);

        // Generate placeholder Dockerfiles for each base
        foreach (var (baseId, baseManifest) in catalog.Bases)
        {
            var dir = Path.Combine(dockerRoot, "bases", baseId);
            Directory.CreateDirectory(dir);
            var dockerfilePath = Path.Combine(dir, "Dockerfile");
            var content = GenerateBaseDockerfile(baseManifest, catalog);
            File.WriteAllText(dockerfilePath, content);
            report.Artifacts.Add(new ArtifactEntry
            {
                Type = "dockerfile",
                Id = $"base-{baseId}",
                Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, dockerfilePath)),
                ContentHash = ContentHasher.ComputeContentHash(content)
            });
            Console.WriteLine($"  [dockerfile] bases/{baseId}/Dockerfile");
        }

        // Generate placeholder Dockerfiles for each combo
        foreach (var (comboId, combo) in catalog.Combos)
        {
            var dir = Path.Combine(dockerRoot, "combos", comboId);
            Directory.CreateDirectory(dir);
            var dockerfilePath = Path.Combine(dir, "Dockerfile");
            var content = GenerateComboDockerfile(combo, catalog);
            File.WriteAllText(dockerfilePath, content);
            report.Artifacts.Add(new ArtifactEntry
            {
                Type = "dockerfile",
                Id = $"combo-{comboId}",
                Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, dockerfilePath)),
                ContentHash = ContentHasher.ComputeContentHash(content)
            });
            Console.WriteLine($"  [dockerfile] combos/{comboId}/Dockerfile");
        }

        // Generate compose fragments
        var composeRoot = Path.Combine(generatedRoot, "compose");
        Directory.CreateDirectory(Path.Combine(composeRoot, "fragments"));

        foreach (var (agentId, agent) in catalog.Agents)
        {
            var fragmentPath = Path.Combine(composeRoot, "fragments", $"{agentId}-service.yaml");
            var content = GenerateAgentComposeFragment(agent);
            File.WriteAllText(fragmentPath, content);
            report.Artifacts.Add(new ArtifactEntry
            {
                Type = "compose-fragment",
                Id = $"agent-{agentId}",
                Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, fragmentPath)),
                ContentHash = ContentHasher.ComputeContentHash(content)
            });
            Console.WriteLine($"  [compose]    fragments/{agentId}-service.yaml");
        }

        // Generate full compose stacks via Scriban templates
        var stackTemplatePath = Path.Combine(templateRoot, "compose", "stack.yaml.scriban");
        if (File.Exists(stackTemplatePath))
        {
            var stackTemplate = File.ReadAllText(stackTemplatePath);
            Directory.CreateDirectory(Path.Combine(composeRoot, "stacks"));

            foreach (var (stackId, stack) in catalog.ComposeStacks)
            {
                var stackDir = Path.Combine(composeRoot, "stacks", stackId);
                Directory.CreateDirectory(stackDir);
                var stackPath = Path.Combine(stackDir, "docker-compose.yaml");
                var content = ComposeStackGenerator.GenerateStack(stack, catalog, stackTemplate);
                File.WriteAllText(stackPath, content);
                report.Artifacts.Add(new ArtifactEntry
                {
                    Type = "compose-stack",
                    Id = $"stack-{stackId}",
                    Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, stackPath)),
                    ContentHash = ContentHasher.ComputeContentHash(content)
                });
                Console.WriteLine($"  [stack]      stacks/{stackId}/docker-compose.yaml");
            }
        }

        // Generate image catalog JSON (deterministic — no timestamp)
        var catalogPath = Path.Combine(generatedRoot, "image-catalog.json");
        var imageCatalog = BuildImageCatalog(catalog, manifestHash);
        var catalogJson = JsonSerializer.Serialize(imageCatalog, JsonOptions);
        File.WriteAllText(catalogPath, catalogJson);
        report.Artifacts.Add(new ArtifactEntry
        {
            Type = "catalog",
            Id = "image-catalog",
            Path = "image-catalog.json",
            ContentHash = ContentHasher.ComputeContentHash(catalogJson)
        });
        Console.WriteLine($"  [catalog]    image-catalog.json");

        return report;
    }

    private static string GenerateBaseDockerfile(
        AgentContainers.Core.Models.BaseRuntimeManifest baseManifest,
        AgentContainers.Core.Models.ManifestCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# AUTO-GENERATED — do not edit by hand.");
        sb.AppendLine($"# Source: definitions/bases/{baseManifest.Id}.yaml");
        sb.AppendLine($"# Generated by AgentContainers.Generator v0.1.0");
        sb.AppendLine();
        sb.AppendLine($"FROM {baseManifest.From.Image}");
        sb.AppendLine();

        // Common tools layer
        if (catalog.CommonTools.TryGetValue(baseManifest.CommonTools, out var tools))
        {
            sb.AppendLine("# Layer 1: Common Tools");
            sb.AppendLine("RUN apt-get update && apt-get install -y --no-install-recommends \\");
            var aptPkgs = tools.Packages.Apt;
            for (int i = 0; i < aptPkgs.Count; i++)
            {
                sb.Append($"    {aptPkgs[i]}");
                sb.AppendLine(i < aptPkgs.Count - 1 ? " \\" : "");
            }
            sb.AppendLine("  && rm -rf /var/lib/apt/lists/*");
            sb.AppendLine();

            foreach (var step in tools.PostInstall)
            {
                sb.AppendLine($"# {step.Description}");
                sb.AppendLine($"RUN {step.Command}");
            }
            if (tools.PostInstall.Count > 0) sb.AppendLine();
        }

        // Base runtime install steps
        if (baseManifest.Install.Steps.Count > 0)
        {
            sb.AppendLine($"# Layer 2: {baseManifest.DisplayName} Runtime");
            foreach (var step in baseManifest.Install.Steps)
            {
                sb.AppendLine($"# {step.Description}");
                sb.AppendLine($"RUN {FormatMultilineRun(step.Run)}");
            }
            sb.AppendLine();
        }

        // Environment variables — ARG/ENV pattern for overridable defaults
        var envVars = baseManifest.Env.Where(e => e.Default != null && !e.Sensitive).ToList();
        if (envVars.Count > 0)
        {
            sb.AppendLine("# Environment defaults (override at build time with --build-arg)");
            foreach (var env in envVars)
            {
                sb.AppendLine($"ARG {env.Name}={env.Default}");
                sb.AppendLine($"ENV {env.Name}=${{{env.Name}}}");
            }
            sb.AppendLine();
        }

        // User setup
        sb.AppendLine($"# User setup");
        sb.AppendLine($"RUN groupadd -g {baseManifest.User.Gid} {baseManifest.User.Name} \\");
        sb.AppendLine($"  && useradd -m -u {baseManifest.User.Uid} -g {baseManifest.User.Gid} {baseManifest.User.Name}");
        if (baseManifest.User.Sudo)
        {
            sb.AppendLine($"RUN echo '{baseManifest.User.Name} ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers.d/{baseManifest.User.Name}");
        }
        sb.AppendLine($"USER {baseManifest.User.Name}");
        sb.AppendLine();

        // Healthcheck
        if (baseManifest.Healthcheck.Test.Count > 0)
        {
            var testStr = string.Join("\", \"", baseManifest.Healthcheck.Test);
            sb.AppendLine($"HEALTHCHECK --interval={baseManifest.Healthcheck.Interval} --timeout={baseManifest.Healthcheck.Timeout} --retries={baseManifest.Healthcheck.Retries} \\");
            sb.AppendLine($"  CMD [\"{testStr}\"]");
        }

        // Labels — OCI image spec + build-time metadata
        sb.AppendLine();
        sb.AppendLine("# OCI Image Labels");
        sb.AppendLine("ARG BUILD_DATE=unknown");
        sb.AppendLine("ARG VCS_REF=unknown");
        sb.AppendLine($"ARG IMAGE_VERSION={baseManifest.Version}");
        sb.AppendLine();
        sb.AppendLine($"LABEL org.opencontainers.image.title=\"{baseManifest.DisplayName}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.description=\"{baseManifest.Description}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.source=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.url=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.vendor=\"AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.licenses=\"MIT\"");
        sb.AppendLine($"LABEL org.opencontainers.image.version=\"${{IMAGE_VERSION}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.created=\"${{BUILD_DATE}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.revision=\"${{VCS_REF}}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.runtime-family=\"{baseManifest.Family}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.image-type=\"base\"");

        return sb.ToString();
    }

    private static string GenerateComboDockerfile(
        AgentContainers.Core.Models.ComboRuntimeManifest combo,
        AgentContainers.Core.Models.ManifestCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# AUTO-GENERATED — do not edit by hand.");
        sb.AppendLine($"# Source: definitions/combos/{combo.Id}.yaml");
        sb.AppendLine($"# Generated by AgentContainers.Generator v0.1.0");
        sb.AppendLine();

        // Use first base's FROM as the starting point
        var primaryBase = combo.Bases.OrderBy(b => b.Order).FirstOrDefault();
        if (primaryBase != null && catalog.Bases.TryGetValue(primaryBase.Id, out var primary))
        {
            sb.AppendLine($"FROM {primary.From.Image}");
        }
        else
        {
            sb.AppendLine("FROM debian:bookworm-slim");
        }
        sb.AppendLine();

        sb.AppendLine($"# Combo: {combo.DisplayName}");
        sb.AppendLine($"# Bases (ordered): {string.Join(" → ", combo.Bases.OrderBy(b => b.Order).Select(b => b.Id))}");
        sb.AppendLine();

        // Merge install steps from all bases in order
        foreach (var baseRef in combo.Bases.OrderBy(b => b.Order))
        {
            if (catalog.Bases.TryGetValue(baseRef.Id, out var baseManifest))
            {
                sb.AppendLine($"# --- {baseManifest.DisplayName} runtime ---");
                foreach (var step in baseManifest.Install.Steps)
                {
                    sb.AppendLine($"# {step.Description}");
                    sb.AppendLine($"RUN {FormatMultilineRun(step.Run)}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("# OCI Image Labels");
        sb.AppendLine("ARG BUILD_DATE=unknown");
        sb.AppendLine("ARG VCS_REF=unknown");
        sb.AppendLine($"ARG IMAGE_VERSION={combo.Version}");
        sb.AppendLine();
        sb.AppendLine($"LABEL org.opencontainers.image.title=\"{combo.DisplayName}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.description=\"{combo.Description}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.source=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.url=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.vendor=\"AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.licenses=\"MIT\"");
        sb.AppendLine($"LABEL org.opencontainers.image.version=\"${{IMAGE_VERSION}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.created=\"${{BUILD_DATE}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.revision=\"${{VCS_REF}}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.image-type=\"combo\"");

        return sb.ToString();
    }

    private static string GenerateAgentComposeFragment(AgentContainers.Core.Models.AgentManifest agent)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# AUTO-GENERATED — do not edit by hand.");
        sb.AppendLine($"# Source: definitions/agents/{agent.Id}.yaml");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine($"  {agent.ComposeCapabilities.ServiceNameDefault}:");
        sb.AppendLine($"    image: ghcr.io/agentcontainers/node-bun-{agent.Id}:latest");
        sb.AppendLine("    environment:");
        foreach (var env in agent.Env)
        {
            if (env.Sensitive)
                sb.AppendLine($"      - {env.Name}=${{{env.Name}:?{env.Name} is required}}");
            else if (env.Default != null)
                sb.AppendLine($"      - {env.Name}={env.Default}");
        }
        if (agent.Mounts.Count > 0)
        {
            sb.AppendLine("    volumes:");
            foreach (var mount in agent.Mounts)
                sb.AppendLine($"      - {mount.Name}:{mount.ContainerPath}");
        }
        sb.AppendLine("    networks:");
        foreach (var net in agent.ComposeCapabilities.Networks)
            sb.AppendLine($"      - {net}");
        if (agent.Healthcheck.Test.Count > 0)
        {
            sb.AppendLine("    healthcheck:");
            sb.AppendLine($"      test: [\"{string.Join("\", \"", agent.Healthcheck.Test)}\"]");
            sb.AppendLine($"      interval: {agent.Healthcheck.Interval}");
            sb.AppendLine($"      timeout: {agent.Healthcheck.Timeout}");
            sb.AppendLine($"      retries: {agent.Healthcheck.Retries}");
            if (agent.Healthcheck.StartPeriod != null)
                sb.AppendLine($"      start_period: {agent.Healthcheck.StartPeriod}");
        }
        sb.AppendLine("    restart: unless-stopped");
        sb.AppendLine("    user: \"1000:1000\"");

        return sb.ToString();
    }

    private static object BuildImageCatalog(AgentContainers.Core.Models.ManifestCatalog catalog, string manifestHash)
    {
        var entries = new List<object>();

        foreach (var (id, b) in catalog.Bases)
        {
            entries.Add(new
            {
                type = "base",
                id,
                display_name = b.DisplayName,
                family = b.Family,
                from_image = b.From.Image,
                provides = b.Provides,
                platforms = b.Platforms,
                registry = $"ghcr.io/agentcontainers/{id}"
            });
        }

        foreach (var (id, c) in catalog.Combos)
        {
            // Derive platforms from primary base
            var primaryBase = c.Bases.OrderBy(b => b.Order).FirstOrDefault();
            var platforms = new List<string> { "linux/amd64" };
            if (primaryBase != null && catalog.Bases.TryGetValue(primaryBase.Id, out var pb) && pb.Platforms.Count > 0)
                platforms = pb.Platforms;

            entries.Add(new
            {
                type = "combo",
                id,
                display_name = c.DisplayName,
                bases = c.Bases.OrderBy(b => b.Order).Select(b => b.Id).ToList(),
                provides = c.Provides,
                platforms,
                registry = $"ghcr.io/agentcontainers/{id}"
            });
        }

        foreach (var (id, a) in catalog.Agents)
        {
            entries.Add(new
            {
                type = "agent",
                id,
                display_name = a.DisplayName,
                requires = a.Requires,
                install_method = a.Install.Method
            });
        }

        return new
        {
            manifest_hash = manifestHash,
            generator_version = "0.1.0",
            registry = "ghcr.io/agentcontainers",
            images = entries
        };
    }

    private static void PrintCatalogSummary(AgentContainers.Core.Models.ManifestCatalog catalog)
    {
        Console.WriteLine($"  Common Tools: {catalog.CommonTools.Count} ({string.Join(", ", catalog.CommonTools.Keys)})");
        Console.WriteLine($"  Bases:        {catalog.Bases.Count} ({string.Join(", ", catalog.Bases.Keys)})");
        Console.WriteLine($"  Combos:       {catalog.Combos.Count} ({string.Join(", ", catalog.Combos.Keys)})");
        Console.WriteLine($"  Agents:       {catalog.Agents.Count} ({string.Join(", ", catalog.Agents.Keys)})");
        Console.WriteLine($"  Tool Packs:   {catalog.ToolPacks.Count} ({string.Join(", ", catalog.ToolPacks.Keys)})");
        Console.WriteLine($"  Compose:      {catalog.ComposeStacks.Count} ({string.Join(", ", catalog.ComposeStacks.Keys)})");
        Console.WriteLine($"  Profiles:     {catalog.Profiles.Count} ({string.Join(", ", catalog.Profiles.Keys)})");
        Console.WriteLine($"  Total:        {catalog.TotalCount}");
    }

    private static void PrintValidationResults(List<ValidationResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("  No issues found.");
            return;
        }

        foreach (var r in results.OrderBy(r => r.Severity))
        {
            var icon = r.Severity switch
            {
                ValidationSeverity.Error => "ERR",
                ValidationSeverity.Warning => "WRN",
                _ => "INF"
            };
            Console.WriteLine($"  [{icon}] [{r.ManifestType}:{r.ManifestId}] {r.Message}");
        }

        var errors = results.Count(r => r.Severity == ValidationSeverity.Error);
        var warnings = results.Count(r => r.Severity == ValidationSeverity.Warning);
        Console.WriteLine($"  Summary: {errors} error(s), {warnings} warning(s), {results.Count - errors - warnings} info(s)");
    }

    private static bool CapabilitySatisfied(string requirement, List<string> provides)
    {
        if (provides.Contains(requirement)) return true;
        var baseToken = requirement.Split(">=")[0].Split("<=")[0];
        return provides.Any(p => p == baseToken || p.StartsWith(baseToken + ">="));
    }

    /// <summary>
    /// Converts a multiline shell block into a single RUN instruction
    /// by joining lines with " &amp;&amp; \\\n    ".
    /// </summary>
    private static string FormatMultilineRun(string run)
    {
        var lines = run.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (lines.Count <= 1)
            return lines.FirstOrDefault() ?? string.Empty;

        return string.Join(" && \\\n    ", lines);
    }

    private static string? FindRepoRoot()
    {
        // Try current directory first, then walk up
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "definitions")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Usage: AgentContainers.Generator <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  generate      Load manifests, validate, and generate artifacts");
        Console.WriteLine("  validate      Load and validate manifests only");
        Console.WriteLine("  list-matrix   Print compatibility matrix");
        Console.WriteLine("  build-matrix  Output JSON build matrix for CI/CD publishing");
        Console.WriteLine();
        return 1;
    }
}

internal sealed class GenerationReport
{
    public string ManifestHash { get; set; } = string.Empty;
    public string GeneratorVersion { get; set; } = string.Empty;
    public List<ArtifactEntry> Artifacts { get; set; } = [];
}

internal sealed class ArtifactEntry
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}
