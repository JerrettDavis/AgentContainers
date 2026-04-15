using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContainers.Core.Hashing;
using AgentContainers.Core.Loading;
using AgentContainers.Core.Matrix;
using AgentContainers.Core.Validation;

namespace AgentContainers.Generator;

/// <summary>
/// Command-line entry point for validating manifests, generating committed artifacts,
/// and emitting machine-readable matrices consumed by CI and e2e automation.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Runs the generator command requested by <paramref name="args"/>.
    /// </summary>
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

        if (!jsonOutput && command is not ("emit-e2e-plan" or "build-matrix"))
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
            "emit-e2e-plan" => RunEmitE2EPlan(repoRoot),
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
        var matrix = BuildPublishMatrix(catalog, manifestHash);

        Console.Write(JsonSerializer.Serialize(matrix, JsonOptions));
        return 0;
    }

    internal static PublishMatrix BuildPublishMatrix(
        AgentContainers.Core.Models.ManifestCatalog catalog,
        string manifestHash)
    {
        var include = new List<PublishMatrixEntry>();

        foreach (var (id, b) in catalog.Bases.OrderBy(entry => entry.Key))
        {
            include.Add(new PublishMatrixEntry
            {
                Id = id,
                Type = "base",
                DisplayName = b.DisplayName,
                Context = $"generated/docker/bases/{id}",
                Dockerfile = "Dockerfile",
                ImageName = $"ghcr.io/${{{{ github.repository_owner }}}}/{id}",
                Platforms = string.Join(",", GetRuntimePlatforms(catalog, id)),
                Family = b.Family,
                ManifestHash = manifestHash
            });
        }

        foreach (var (id, c) in catalog.Combos.OrderBy(entry => entry.Key))
        {
            include.Add(new PublishMatrixEntry
            {
                Id = id,
                Type = "combo",
                DisplayName = c.DisplayName,
                Context = $"generated/docker/combos/{id}",
                Dockerfile = "Dockerfile",
                ImageName = $"ghcr.io/${{{{ github.repository_owner }}}}/{id}",
                Platforms = string.Join(",", GetRuntimePlatforms(catalog, id)),
                Family = string.Join("+", c.Bases.OrderBy(b => b.Order).Select(b => b.Id)),
                ManifestHash = manifestHash
            });
        }

        foreach (var (agentId, agent) in catalog.Agents.OrderBy(entry => entry.Key))
        {
            foreach (var (runtimeId, runtimeDisplayName) in GetCompatibleRuntimeTargets(catalog, agent))
            {
                include.Add(new PublishMatrixEntry
                {
                    Id = $"{runtimeId}-{agentId}",
                    Type = "agent-image",
                    DisplayName = $"{agent.DisplayName} on {runtimeDisplayName}",
                    Context = $"generated/docker/agents/{runtimeId}-{agentId}",
                    Dockerfile = "Dockerfile",
                    ImageName = $"ghcr.io/${{{{ github.repository_owner }}}}/{runtimeId}-{agentId}",
                    Platforms = string.Join(",", GetRuntimePlatforms(catalog, runtimeId)),
                    BaseId = runtimeId,
                    AgentId = agentId,
                    ManifestHash = manifestHash
                });
            }
        }

        foreach (var (toolPackId, toolPack) in catalog.ToolPacks.OrderBy(entry => entry.Key))
        {
            if (toolPack.Sidecar?.Enabled == true)
                continue;

            foreach (var runtimeId in toolPack.CompatibleWith.Bases.OrderBy(id => id))
            {
                if (!TryGetRuntimeDisplayName(catalog, runtimeId, out var runtimeDisplayName))
                    continue;

                include.Add(new PublishMatrixEntry
                {
                    Id = $"{runtimeId}-{toolPackId}",
                    Type = "tool-pack-image",
                    DisplayName = $"{toolPack.DisplayName} on {runtimeDisplayName}",
                    Context = $"generated/docker/tool-packs/{runtimeId}-{toolPackId}",
                    Dockerfile = "Dockerfile",
                    ImageName = $"ghcr.io/${{{{ github.repository_owner }}}}/{runtimeId}-{toolPackId}",
                    Platforms = string.Join(",", GetRuntimePlatforms(catalog, runtimeId)),
                    BaseId = runtimeId,
                    ToolPackId = toolPackId,
                    ManifestHash = manifestHash
                });
            }
        }

        return new PublishMatrix
        {
            Include = include
        };
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

        // Generate agent overlay Dockerfiles for compatible base×agent pairs
        foreach (var (agentId, agent) in catalog.Agents)
        {
            var installCmd = GetAgentInstallCommand(agent);
            if (installCmd == null) continue;

            // Agent overlays on base runtimes
            foreach (var (baseId, baseManifest) in catalog.Bases)
            {
                if (!agent.Requires.All(req => CapabilitySatisfied(req, baseManifest.Provides)))
                    continue;

                var imageId = $"{baseId}-{agentId}";
                var dir = Path.Combine(dockerRoot, "agents", imageId);
                Directory.CreateDirectory(dir);
                var dockerfilePath = Path.Combine(dir, "Dockerfile");
                var content = GenerateAgentDockerfile(agent, baseId, baseManifest.DisplayName, installCmd);
                File.WriteAllText(dockerfilePath, content);
                report.Artifacts.Add(new ArtifactEntry
                {
                    Type = "dockerfile",
                    Id = $"agent-{imageId}",
                    Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, dockerfilePath)),
                    ContentHash = ContentHasher.ComputeContentHash(content)
                });
                Console.WriteLine($"  [dockerfile] agents/{imageId}/Dockerfile");
            }

            // Agent overlays on combo runtimes
            foreach (var (comboId, combo) in catalog.Combos)
            {
                if (!agent.Requires.All(req => CapabilitySatisfied(req, combo.Provides)))
                    continue;

                var imageId = $"{comboId}-{agentId}";
                var dir = Path.Combine(dockerRoot, "agents", imageId);
                Directory.CreateDirectory(dir);
                var dockerfilePath = Path.Combine(dir, "Dockerfile");
                var content = GenerateAgentDockerfile(agent, comboId, combo.DisplayName, installCmd);
                File.WriteAllText(dockerfilePath, content);
                report.Artifacts.Add(new ArtifactEntry
                {
                    Type = "dockerfile",
                    Id = $"agent-{imageId}",
                    Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, dockerfilePath)),
                    ContentHash = ContentHasher.ComputeContentHash(content)
                });
                Console.WriteLine($"  [dockerfile] agents/{imageId}/Dockerfile");
            }
        }

        // Generate tool-pack overlay Dockerfiles (non-sidecar packs only)
        foreach (var (packId, pack) in catalog.ToolPacks)
        {
            if (pack.Sidecar?.Enabled == true) continue;

            foreach (var compatBaseId in pack.CompatibleWith.Bases)
            {
                string baseDisplayName;
                if (catalog.Bases.TryGetValue(compatBaseId, out var bm))
                    baseDisplayName = bm.DisplayName;
                else if (catalog.Combos.TryGetValue(compatBaseId, out var cm))
                    baseDisplayName = cm.DisplayName;
                else
                    continue;

                var imageId = $"{compatBaseId}-{packId}";
                var dir = Path.Combine(dockerRoot, "tool-packs", imageId);
                Directory.CreateDirectory(dir);
                var dockerfilePath = Path.Combine(dir, "Dockerfile");
                var content = GenerateToolPackDockerfile(pack, compatBaseId, baseDisplayName);
                File.WriteAllText(dockerfilePath, content);
                report.Artifacts.Add(new ArtifactEntry
                {
                    Type = "dockerfile",
                    Id = $"tool-pack-{imageId}",
                    Path = ContentHasher.NormalizePath(Path.GetRelativePath(generatedRoot, dockerfilePath)),
                    ContentHash = ContentHasher.ComputeContentHash(content)
                });
                Console.WriteLine($"  [dockerfile] tool-packs/{imageId}/Dockerfile");
            }
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
            sb.AppendLine(FormatAptInstall(tools.Packages.Apt));
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

        if (string.Equals(baseManifest.Family, "rust", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("# Ensure Rust toolchain binaries are on PATH for the runtime user");
            sb.AppendLine("ENV PATH=\"/usr/local/cargo/bin:${PATH}\"");
            sb.AppendLine();
        }

        // User setup (idempotent — handles base images that already have a group/user at the target GID/UID)
        sb.AppendLine($"# User setup");
        sb.AppendLine($"RUN groupadd -g {baseManifest.User.Gid} {baseManifest.User.Name} 2>/dev/null || true \\");
        sb.AppendLine($"  && useradd -m -u {baseManifest.User.Uid} -g {baseManifest.User.Gid} {baseManifest.User.Name} 2>/dev/null \\");
        sb.AppendLine($"  || id -u {baseManifest.User.Name} >/dev/null 2>&1 \\");
        sb.AppendLine($"  || (useradd -m -o -u {baseManifest.User.Uid} -g {baseManifest.User.Gid} {baseManifest.User.Name})");
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

        var primaryBase = combo.Bases.OrderBy(b => b.Order).FirstOrDefault();
        if (primaryBase != null && catalog.Bases.TryGetValue(primaryBase.Id, out var primary))
        {
            sb.AppendLine($"ARG PRIMARY_BASE_IMAGE=agentcontainers/{primaryBase.Id}:latest");
            foreach (var baseRef in combo.Bases.OrderBy(b => b.Order).Skip(1))
            {
                if (catalog.Bases.TryGetValue(baseRef.Id, out var stageBase))
                {
                    sb.AppendLine($"ARG {stageBase.Id.ToUpperInvariant().Replace('-', '_')}_STAGE_IMAGE=agentcontainers/{stageBase.Id}:latest");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"FROM ${{PRIMARY_BASE_IMAGE}} AS runtime");
            sb.AppendLine();

            foreach (var baseRef in combo.Bases.OrderBy(b => b.Order).Skip(1))
            {
                if (catalog.Bases.TryGetValue(baseRef.Id, out var stageBase))
                {
                    sb.AppendLine($"FROM ${{{stageBase.Id.ToUpperInvariant().Replace('-', '_')}_STAGE_IMAGE}} AS {stageBase.Id.Replace('-', '_')}_stage");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("FROM runtime");
        }
        else
        {
            sb.AppendLine("FROM debian:bookworm-slim");
        }
        sb.AppendLine();

        sb.AppendLine($"# Combo: {combo.DisplayName}");
        sb.AppendLine($"# Bases (ordered): {string.Join(" → ", combo.Bases.OrderBy(b => b.Order).Select(b => b.Id))}");
        sb.AppendLine();

        if (primaryBase != null)
        {
            sb.AppendLine("USER root");
            sb.AppendLine();
        }

        foreach (var baseRef in combo.Bases.OrderBy(b => b.Order).Skip(1))
        {
            if (catalog.Bases.TryGetValue(baseRef.Id, out var baseManifest))
            {
                var stageName = $"{baseManifest.Id.Replace('-', '_')}_stage";
                sb.AppendLine($"# --- Import runtime from {baseManifest.DisplayName} base ---");

                if (baseManifest.Provides.Any(p => p == "rust" || p.StartsWith("rust>=")))
                {
                    sb.AppendLine($"COPY --from={stageName} /usr/local/cargo /usr/local/cargo");
                    sb.AppendLine($"COPY --from={stageName} /usr/local/rustup /usr/local/rustup");
                    sb.AppendLine("ENV CARGO_HOME=/usr/local/cargo");
                    sb.AppendLine("ENV RUSTUP_HOME=/usr/local/rustup");
                    sb.AppendLine("ENV PATH=\"/usr/local/cargo/bin:${PATH}\"");
                }

                if (baseManifest.Provides.Any(p => p == "python" || p.StartsWith("python>=")))
                {
                    sb.AppendLine($"COPY --from={stageName} /usr/local /usr/local");
                }

                if (baseManifest.Provides.Any(p => p == "dotnet" || p.StartsWith("dotnet>=")))
                {
                    sb.AppendLine($"COPY --from={stageName} /usr/share/dotnet /usr/share/dotnet");
                    sb.AppendLine(FormatAptInstall(["libicu-dev"]));
                    sb.AppendLine("RUN ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet");
                }

                if (baseManifest.Install.Steps.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"# --- {baseManifest.DisplayName} runtime setup ---");
                    foreach (var step in baseManifest.Install.Steps)
                    {
                        sb.AppendLine($"# {step.Description}");
                        sb.AppendLine($"RUN {FormatMultilineRun(step.Run)}");
                    }
                }
                sb.AppendLine();
            }
        }

        if (catalog.CommonTools.TryGetValue("default", out var commonTools) && commonTools.PostInstall.Count > 0)
        {
            sb.AppendLine("# Re-apply common tool path helpers");
            foreach (var step in commonTools.PostInstall)
            {
                sb.AppendLine($"# {step.Description}");
                sb.AppendLine($"RUN {step.Command}");
            }
            sb.AppendLine();
        }

        if (combo.CrossRuntimeUtilities.Apt.Count > 0)
        {
            sb.AppendLine("# Cross-runtime utilities");
            sb.AppendLine(FormatAptInstall(combo.CrossRuntimeUtilities.Apt));
            sb.AppendLine();
        }

        sb.AppendLine("USER dev");
        sb.AppendLine();

        if (combo.Validation.Commands.Count > 0)
        {
            var comboValidation = combo.Validation.Commands.First();
            sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=10s --retries=3 \\");
            sb.AppendLine($"  CMD [\"CMD-SHELL\", \"{comboValidation} || exit 1\"]");
            sb.AppendLine();
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

            // Agent overlay images on compatible bases
            foreach (var (baseId, baseManifest) in catalog.Bases)
            {
                if (!a.Requires.All(req => CapabilitySatisfied(req, baseManifest.Provides)))
                    continue;

                var imageId = $"{baseId}-{id}";
                entries.Add(new
                {
                    type = "agent-image",
                    id = imageId,
                    display_name = $"{a.DisplayName} on {baseManifest.DisplayName}",
                    requires = a.Requires,
                    install_method = a.Install.Method
                });
            }

            // Agent overlay images on compatible combos
            foreach (var (comboId, combo) in catalog.Combos)
            {
                if (!a.Requires.All(req => CapabilitySatisfied(req, combo.Provides)))
                    continue;

                var imageId = $"{comboId}-{id}";
                entries.Add(new
                {
                    type = "agent-image",
                    id = imageId,
                    display_name = $"{a.DisplayName} on {combo.DisplayName}",
                    requires = a.Requires,
                    install_method = a.Install.Method
                });
            }
        }

        // Tool-pack overlay images (non-sidecar)
        foreach (var (id, tp) in catalog.ToolPacks)
        {
            if (tp.Sidecar?.Enabled == true) continue;

            foreach (var compatBaseId in tp.CompatibleWith.Bases)
            {
                var imageId = $"{compatBaseId}-{id}";
                entries.Add(new
                {
                    type = "tool-pack-image",
                    id = imageId,
                    display_name = $"{tp.DisplayName} on {compatBaseId}",
                    tool_pack = id,
                    base_id = compatBaseId,
                    registry = $"ghcr.io/agentcontainers/{imageId}"
                });
            }
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

    private static string FormatAptInstall(IEnumerable<string> packages)
    {
        var packageList = packages.Where(static p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (packageList.Count == 0)
            throw new InvalidOperationException("Apt install generation requires at least one package.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends \\");
        for (var i = 0; i < packageList.Count; i++)
        {
            sb.Append($"    {packageList[i]}");
            sb.AppendLine(i < packageList.Count - 1 ? " \\" : " \\");
        }
        sb.Append("  && rm -rf /var/lib/apt/lists/*");
        return sb.ToString();
    }

    private static void AppendHealthcheck(
        System.Text.StringBuilder sb,
        AgentContainers.Core.Models.HealthcheckConfig healthcheck)
    {
        if (healthcheck.Test.Count == 0)
            return;

        var testStr = string.Join("\", \"", healthcheck.Test);
        sb.AppendLine($"HEALTHCHECK --interval={healthcheck.Interval} --timeout={healthcheck.Timeout} --retries={healthcheck.Retries} \\");
        sb.AppendLine($"  CMD [\"{testStr}\"]");
        sb.AppendLine();
    }

    private static void AppendValidationHealthcheck(System.Text.StringBuilder sb, IEnumerable<string> commands)
    {
        var firstCommand = commands.FirstOrDefault(static command => !string.IsNullOrWhiteSpace(command));
        if (string.IsNullOrWhiteSpace(firstCommand))
            return;

        var normalizedCommand = firstCommand
            .Replace(" || true", string.Empty, StringComparison.Ordinal)
            .Replace("|| true", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return;

        sb.AppendLine("HEALTHCHECK --interval=30s --timeout=10s --retries=3 \\");
        sb.AppendLine($"  CMD [\"CMD-SHELL\", \"{normalizedCommand} || exit 1\"]");
        sb.AppendLine();
    }

    private static int RunEmitE2EPlan(string repoRoot)
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(Path.Combine(repoRoot, "definitions"));

        var plan = new E2EPlan();

        // Common-tool validation commands (applied to every base image)
        var commonValidations = new List<string>();
        if (catalog.CommonTools.TryGetValue("default", out var defaultTools))
            commonValidations = defaultTools.Validation.Commands;

        // Base images
        foreach (var (id, b) in catalog.Bases)
        {
            plan.Bases.Add(new E2EImageTestCase
            {
                Id = id,
                DisplayName = b.DisplayName,
                BuildContext = $"generated/docker/bases/{id}",
                Tag = $"agentcontainers/{id}:latest",
                ValidationCommands = b.Validation.Commands,
                CommonToolValidations = commonValidations,
                SizeClass = b.ResourceHints.ImageSizeClass
            });
        }

        // Combo images
        foreach (var (id, c) in catalog.Combos)
        {
            plan.Combos.Add(new E2EImageTestCase
            {
                Id = id,
                DisplayName = c.DisplayName,
                BuildContext = $"generated/docker/combos/{id}",
                Tag = $"agentcontainers/{id}:latest",
                ValidationCommands = c.Validation.Commands,
                CommonToolValidations = [],
                SizeClass = c.ResourceHints.ImageSizeClass
            });
        }

        // Agent overlay images (compatible base×agent)
        foreach (var (agentId, agent) in catalog.Agents)
        {
            foreach (var (baseId, baseManifest) in catalog.Bases)
            {
                if (!agent.Requires.All(req => CapabilitySatisfied(req, baseManifest.Provides)))
                    continue;

                var imageId = $"{baseId}-{agentId}";
                plan.Agents.Add(new E2EAgentTestCase
                {
                    Id = imageId,
                    AgentId = agentId,
                    BaseId = baseId,
                    DisplayName = $"{agent.DisplayName} on {baseManifest.DisplayName}",
                    BuildContext = $"generated/docker/agents/{imageId}",
                    Tag = $"agentcontainers/{imageId}:latest",
                    BaseTag = $"agentcontainers/{baseId}:latest",
                    ValidationCommands = agent.Validation.Commands,
                    SizeClass = baseManifest.ResourceHints.ImageSizeClass
                });
            }

            foreach (var (comboId, combo) in catalog.Combos)
            {
                if (!agent.Requires.All(req => CapabilitySatisfied(req, combo.Provides)))
                    continue;

                var imageId = $"{comboId}-{agentId}";
                plan.Agents.Add(new E2EAgentTestCase
                {
                    Id = imageId,
                    AgentId = agentId,
                    BaseId = comboId,
                    DisplayName = $"{agent.DisplayName} on {combo.DisplayName}",
                    BuildContext = $"generated/docker/agents/{imageId}",
                    Tag = $"agentcontainers/{imageId}:latest",
                    BaseTag = $"agentcontainers/{comboId}:latest",
                    ValidationCommands = agent.Validation.Commands,
                    SizeClass = combo.ResourceHints.ImageSizeClass
                });
            }
        }

        // Tool pack overlay images (non-sidecar packs on compatible bases)
        foreach (var (packId, pack) in catalog.ToolPacks)
        {
            if (pack.Sidecar?.Enabled == true) continue;

            foreach (var compatBaseId in pack.CompatibleWith.Bases)
            {
                string sizeClass = "large";
                if (catalog.Bases.TryGetValue(compatBaseId, out var bm))
                    sizeClass = bm.ResourceHints.ImageSizeClass;
                else if (catalog.Combos.TryGetValue(compatBaseId, out var cm))
                    sizeClass = cm.ResourceHints.ImageSizeClass;

                var imageId = $"{compatBaseId}-{packId}";
                plan.ToolPacks.Add(new E2EToolPackTestCase
                {
                    Id = imageId,
                    PackId = packId,
                    BaseId = compatBaseId,
                    DisplayName = $"{pack.DisplayName} on {compatBaseId}",
                    BuildContext = $"generated/docker/tool-packs/{imageId}",
                    Tag = $"agentcontainers/{imageId}:latest",
                    BaseTag = $"agentcontainers/{compatBaseId}:latest",
                    ValidationCommands = pack.Validation.Commands,
                    SizeClass = sizeClass
                });
            }
        }

        // Compose stacks
        foreach (var (stackId, stack) in catalog.ComposeStacks)
        {
            var requiredImages = new List<string>();
            foreach (var svc in stack.Services)
            {
                if (svc.Agent != null)
                {
                    var baseName = svc.Base ?? "node-bun";
                    requiredImages.Add($"agentcontainers/{baseName}-{svc.Agent}:latest");
                }
            }

            plan.ComposeStacks.Add(new E2EComposeTestCase
            {
                Id = stackId,
                DisplayName = stack.DisplayName,
                ComposePath = $"generated/compose/stacks/{stackId}/docker-compose.yaml",
                RequiredImages = requiredImages
            });
        }

        Console.Write(JsonSerializer.Serialize(plan, JsonOptions));
        return 0;
    }

    private static string GenerateAgentDockerfile(
        AgentContainers.Core.Models.AgentManifest agent,
        string baseId,
        string baseDisplayName,
        string installCommand)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# AUTO-GENERATED — do not edit by hand.");
        sb.AppendLine($"# Agent overlay: {agent.Id} on {baseId}");
        sb.AppendLine($"# Generated by AgentContainers.Generator v0.1.0");
        sb.AppendLine();
        sb.AppendLine($"ARG BASE_IMAGE=agentcontainers/{baseId}:latest");
        sb.AppendLine($"FROM ${{BASE_IMAGE}}");
        sb.AppendLine();
        sb.AppendLine("USER root");
        sb.AppendLine();
        sb.AppendLine($"# Install {agent.DisplayName} via {agent.Install.Method}");
        sb.AppendLine($"RUN {installCommand}");
        sb.AppendLine();

        // Post-install verification steps
        foreach (var step in agent.Install.PostInstall)
        {
            sb.AppendLine($"# {step.Description}");
            sb.AppendLine($"RUN {step.Command}");
        }
        if (agent.Install.PostInstall.Count > 0)
            sb.AppendLine();

        sb.AppendLine("USER dev");
        sb.AppendLine();
        if (agent.Healthcheck.Test.Count > 0)
            AppendHealthcheck(sb, agent.Healthcheck);
        else
            AppendValidationHealthcheck(sb, agent.Validation.Commands);

        // OCI Labels
        sb.AppendLine("# OCI Image Labels");
        sb.AppendLine($"ARG BUILD_DATE=unknown");
        sb.AppendLine($"ARG VCS_REF=unknown");
        sb.AppendLine($"ARG IMAGE_VERSION={agent.Version}");
        sb.AppendLine();
        sb.AppendLine($"LABEL org.opencontainers.image.title=\"{agent.DisplayName} on {baseDisplayName}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.description=\"{agent.Description}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.source=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.vendor=\"AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.licenses=\"MIT\"");
        sb.AppendLine($"LABEL org.opencontainers.image.version=\"${{IMAGE_VERSION}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.created=\"${{BUILD_DATE}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.revision=\"${{VCS_REF}}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.image-type=\"agent\"");
        sb.AppendLine($"LABEL dev.agentcontainers.agent=\"{agent.Id}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.base=\"{baseId}\"");

        return sb.ToString();
    }

    private static string GenerateToolPackDockerfile(
        AgentContainers.Core.Models.ToolPackManifest toolPack,
        string baseId,
        string baseDisplayName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# AUTO-GENERATED — do not edit by hand.");
        sb.AppendLine($"# Tool pack overlay: {toolPack.Id} on {baseId}");
        sb.AppendLine($"# Generated by AgentContainers.Generator v0.1.0");
        sb.AppendLine();
        sb.AppendLine($"ARG BASE_IMAGE=agentcontainers/{baseId}:latest");
        sb.AppendLine($"FROM ${{BASE_IMAGE}}");
        sb.AppendLine();
        sb.AppendLine("USER root");
        sb.AppendLine();
        sb.AppendLine($"# Install {toolPack.DisplayName}");

        // Main install command if package is specified
        if (!string.IsNullOrEmpty(toolPack.Install.Package))
        {
            var pkg = toolPack.Install.Package;
            var version = toolPack.Install.Version;
            var versionSuffix = string.IsNullOrEmpty(version) || version == "latest" ? "" : $"@{version}";
            var installCmd = toolPack.Install.Method switch
            {
                "npm_global" => $"npm install -g {pkg}{versionSuffix}",
                "pip" => $"pip install --no-cache-dir {pkg}{versionSuffix}",
                "apt" => FormatAptInstall([pkg]),
                _ => null
            };
            if (installCmd != null)
            {
                sb.AppendLine($"RUN {installCmd}");
                sb.AppendLine();
            }
        }

        // Post-install steps (tool-pack commands are complete shell statements, used as-is)
        foreach (var step in toolPack.Install.PostInstall)
        {
            sb.AppendLine($"# {step.Description}");
            sb.AppendLine($"RUN {step.Command.Trim()}");
        }
        if (toolPack.Install.PostInstall.Count > 0)
            sb.AppendLine();

        // Environment variables
        var envVars = toolPack.Env.Where(e => e.Default != null && !e.Sensitive).ToList();
        if (envVars.Count > 0)
        {
            sb.AppendLine("# Environment defaults");
            foreach (var env in envVars)
            {
                sb.AppendLine($"ARG {env.Name}={env.Default}");
                sb.AppendLine($"ENV {env.Name}=${{{env.Name}}}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("USER dev");
        sb.AppendLine();
        AppendValidationHealthcheck(sb, toolPack.Validation.Commands);

        // OCI Labels
        sb.AppendLine("# OCI Image Labels");
        sb.AppendLine("ARG BUILD_DATE=unknown");
        sb.AppendLine("ARG VCS_REF=unknown");
        sb.AppendLine($"ARG IMAGE_VERSION={toolPack.Version}");
        sb.AppendLine();
        sb.AppendLine($"LABEL org.opencontainers.image.title=\"{toolPack.DisplayName} on {baseDisplayName}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.description=\"{toolPack.Description}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.source=\"https://github.com/agentcontainers/AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.vendor=\"AgentContainers\"");
        sb.AppendLine($"LABEL org.opencontainers.image.licenses=\"MIT\"");
        sb.AppendLine($"LABEL org.opencontainers.image.version=\"${{IMAGE_VERSION}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.created=\"${{BUILD_DATE}}\"");
        sb.AppendLine($"LABEL org.opencontainers.image.revision=\"${{VCS_REF}}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.image-type=\"tool-pack\"");
        sb.AppendLine($"LABEL dev.agentcontainers.tool-pack=\"{toolPack.Id}\"");
        sb.AppendLine($"LABEL dev.agentcontainers.base=\"{baseId}\"");

        return sb.ToString();
    }

    private static string? GetAgentInstallCommand(AgentContainers.Core.Models.AgentManifest agent)
    {
        var pkg = agent.Install.Package;
        var version = agent.Install.Version;
        var versionSuffix = string.IsNullOrEmpty(version) || version == "latest" ? "" : $"@{version}";

        return agent.Install.Method switch
        {
            "npm_global" => $"npm install -g {pkg}{versionSuffix}",
            "pip" => $"pip install --no-cache-dir {pkg}{versionSuffix}",
            "apt" => FormatAptInstall([pkg]),
            _ => null
        };
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

    private static IEnumerable<(string RuntimeId, string RuntimeDisplayName)> GetCompatibleRuntimeTargets(
        AgentContainers.Core.Models.ManifestCatalog catalog,
        AgentContainers.Core.Models.AgentManifest agent)
    {
        foreach (var (baseId, baseManifest) in catalog.Bases.OrderBy(entry => entry.Key))
        {
            if (agent.Requires.All(req => CapabilitySatisfied(req, baseManifest.Provides)))
                yield return (baseId, baseManifest.DisplayName);
        }

        foreach (var (comboId, combo) in catalog.Combos.OrderBy(entry => entry.Key))
        {
            if (agent.Requires.All(req => CapabilitySatisfied(req, combo.Provides)))
                yield return (comboId, combo.DisplayName);
        }
    }

    private static IReadOnlyList<string> GetRuntimePlatforms(
        AgentContainers.Core.Models.ManifestCatalog catalog,
        string runtimeId)
    {
        if (catalog.Bases.TryGetValue(runtimeId, out var runtimeBase))
            return runtimeBase.Platforms.Count > 0 ? runtimeBase.Platforms : ["linux/amd64"];

        if (catalog.Combos.TryGetValue(runtimeId, out var combo))
        {
            var primaryBaseId = combo.Bases
                .OrderBy(b => b.Order)
                .Select(b => b.Id)
                .FirstOrDefault();

            if (primaryBaseId != null && catalog.Bases.TryGetValue(primaryBaseId, out var primaryBase))
                return primaryBase.Platforms.Count > 0 ? primaryBase.Platforms : ["linux/amd64"];
        }

        return ["linux/amd64"];
    }

    private static bool TryGetRuntimeDisplayName(
        AgentContainers.Core.Models.ManifestCatalog catalog,
        string runtimeId,
        out string displayName)
    {
        if (catalog.Bases.TryGetValue(runtimeId, out var runtimeBase))
        {
            displayName = runtimeBase.DisplayName;
            return true;
        }

        if (catalog.Combos.TryGetValue(runtimeId, out var combo))
        {
            displayName = combo.DisplayName;
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    internal sealed class PublishMatrix
    {
        public List<PublishMatrixEntry> Include { get; init; } = [];
    }

    internal sealed class PublishMatrixEntry
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required string DisplayName { get; init; }
        public required string Context { get; init; }
        public required string Dockerfile { get; init; }
        public required string ImageName { get; init; }
        public required string Platforms { get; init; }
        public string? Family { get; init; }
        public string? BaseId { get; init; }
        public string? AgentId { get; init; }
        public string? ToolPackId { get; init; }
        public required string ManifestHash { get; init; }
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Usage: AgentContainers.Generator <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  generate        Load manifests, validate, and generate artifacts");
        Console.WriteLine("  validate        Load and validate manifests only");
        Console.WriteLine("  list-matrix     Print compatibility matrix");
        Console.WriteLine("  build-matrix    Output JSON build matrix for CI/CD publishing");
        Console.WriteLine("  emit-e2e-plan   Output JSON test plan for e2e container validation");
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

#region E2E Plan Models

internal sealed class E2EPlan
{
    public List<E2EImageTestCase> Bases { get; set; } = [];
    public List<E2EImageTestCase> Combos { get; set; } = [];
    public List<E2EAgentTestCase> Agents { get; set; } = [];
    public List<E2EToolPackTestCase> ToolPacks { get; set; } = [];
    public List<E2EComposeTestCase> ComposeStacks { get; set; } = [];
}

internal sealed class E2EImageTestCase
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BuildContext { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public List<string> ValidationCommands { get; set; } = [];
    public List<string> CommonToolValidations { get; set; } = [];
    public string SizeClass { get; set; } = "medium";
}

internal sealed class E2EAgentTestCase
{
    public string Id { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string BaseId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BuildContext { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string BaseTag { get; set; } = string.Empty;
    public List<string> ValidationCommands { get; set; } = [];
    public string SizeClass { get; set; } = "medium";
}

internal sealed class E2EToolPackTestCase
{
    public string Id { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string BaseId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BuildContext { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string BaseTag { get; set; } = string.Empty;
    public List<string> ValidationCommands { get; set; } = [];
    public string SizeClass { get; set; } = "medium";
}

internal sealed class E2EComposeTestCase
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ComposePath { get; set; } = string.Empty;
    public List<string> RequiredImages { get; set; } = [];
}

#endregion
