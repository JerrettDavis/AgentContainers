using AgentContainers.Core.Loading;
using AgentContainers.Core.Models;
using AgentContainers.Generator;

namespace AgentContainers.Tests;

/// <summary>
/// Tests for ComposeStackGenerator — full stack generation from compose manifests.
/// </summary>
public class ComposeStackGeneratorTests
{
    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "definitions");
            if (Directory.Exists(candidate))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static ManifestCatalog LoadCatalog()
    {
        return new ManifestLoader().LoadAll(Path.Combine(GetRepoRoot(), "definitions"));
    }

    private static string LoadStackTemplate()
    {
        return File.ReadAllText(
            Path.Combine(GetRepoRoot(), "templates", "compose", "stack.yaml.scriban"));
    }

    [Fact]
    public void ResolveServices_SoloClaude_HasOneService()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        Assert.Single(services);
        Assert.Equal("claude", services[0].Name);
    }

    [Fact]
    public void ResolveServices_SoloClaude_ResolvesAgentImage()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        Assert.Equal("ghcr.io/agentcontainers/node-bun-claude:latest", services[0].Image);
    }

    [Fact]
    public void ResolveServices_SoloClaude_IncludesEnvironment()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        // Claude requires ANTHROPIC_API_KEY (sensitive)
        Assert.Contains(services[0].Environment,
            e => e.Contains("ANTHROPIC_API_KEY"));
    }

    [Fact]
    public void ResolveServices_SoloClaude_IncludesVolumes()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        Assert.NotEmpty(services[0].Volumes);
        Assert.Contains(services[0].Volumes, v => v.Contains("workspace"));
    }

    [Fact]
    public void ResolveServices_SoloClaude_IncludesHealthcheck()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        Assert.NotNull(services[0].Healthcheck);
        Assert.NotEmpty(services[0].Healthcheck!.Test);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_HasThreeServices()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        Assert.Equal(3, services.Count);
        Assert.Equal("openclaw", services[0].Name);
        Assert.Equal("headroom", services[1].Name);
        Assert.Equal("claude", services[2].Name);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_SidecarHasExplicitImage()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var headroom = services.First(s => s.Name == "headroom");
        Assert.Equal("headroom/headroom:latest", headroom.Image);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_SidecarHasPorts()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var headroom = services.First(s => s.Name == "headroom");
        Assert.Contains("8787:8787", headroom.Ports);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_OptionalServiceGetsProfile()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var claude = services.First(s => s.Name == "claude");
        Assert.Contains("claude", claude.Profiles);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_DependenciesResolved()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var openclaw = services.First(s => s.Name == "openclaw");
        Assert.Single(openclaw.DependsOn);
        Assert.Equal("headroom", openclaw.DependsOn[0].Id);
        Assert.Equal("service_healthy", openclaw.DependsOn[0].Condition);
    }

    [Fact]
    public void GenerateStack_SoloClaude_RendersScriban()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("services:", output);
        Assert.Contains("claude:", output);
        Assert.Contains("image: ghcr.io/agentcontainers/node-bun-claude:latest", output);
        Assert.Contains("networks:", output);
        Assert.Contains("volumes:", output);
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_ContainsAllServices()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("openclaw:", output);
        Assert.Contains("headroom:", output);
        Assert.Contains("claude:", output);
        Assert.Contains("headroom/headroom:latest", output);
    }

    [Fact]
    public void GenerateStack_IsDeterministic()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var template = LoadStackTemplate();

        var output1 = ComposeStackGenerator.GenerateStack(stack, catalog, template);
        var output2 = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Equal(output1, output2);
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_ContainsDependsOn()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("depends_on:", output);
        Assert.Contains("condition: service_healthy", output);
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_ContainsProfiles()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("profiles:", output);
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_SidecarHasEnvironment()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var headroom = services.First(s => s.Name == "headroom");
        // Sidecar env vars wired from tool-pack sidecar_env
        Assert.NotEmpty(headroom.Environment);
        Assert.Contains(headroom.Environment, e => e.Contains("HEADROOM_HOST"));
        Assert.Contains(headroom.Environment, e => e.Contains("HEADROOM_PORT"));
        Assert.Contains(headroom.Environment, e => e.Contains("HEADROOM_MODE"));
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_SidecarGuardsSensitiveVars()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var headroom = services.First(s => s.Name == "headroom");
        // ANTHROPIC_API_KEY is sensitive/optional → should use ${VAR:-} pattern
        Assert.Contains(headroom.Environment, e => e.StartsWith("ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY:-}"));
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_SidecarUsesOverrideDefaults()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var headroom = services.First(s => s.Name == "headroom");
        // Non-sensitive sidecar env vars use ${VAR:-default} override pattern
        Assert.Contains(headroom.Environment, e => e == "HEADROOM_HOST=${HEADROOM_HOST:-0.0.0.0}");
        Assert.Contains(headroom.Environment, e => e == "HEADROOM_PORT=${HEADROOM_PORT:-8787}");
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_AgentGetsToolPackClientEnv()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        var openclaw = services.First(s => s.Name == "openclaw");
        // OpenClaw has toolpacks: [headroom], should get HEADROOM_PROXY_URL
        Assert.Contains(openclaw.Environment, e => e.Contains("HEADROOM_PROXY_URL"));
    }

    [Fact]
    public void ResolveServices_GatewayHeadroom_EnvFileSet()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        // All services should have env_file from stack-level setting
        Assert.All(services, s => Assert.Equal(".env", s.EnvFile));
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_ContainsEnvFile()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("env_file:", output);
        Assert.Contains(".env", output);
    }

    [Fact]
    public void ResolveServices_SoloClaude_NoEnvFile()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["solo-claude"];
        var services = ComposeStackGenerator.ResolveServices(stack, catalog);

        // solo-claude has no env_file, should be null
        Assert.All(services, s => Assert.Null(s.EnvFile));
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_HeadroomCorrectPort()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("8787:8787", output);
        Assert.DoesNotContain("8080", output);
    }

    [Fact]
    public void GenerateStack_GatewayHeadroom_HeadroomReadyzHealthcheck()
    {
        var catalog = LoadCatalog();
        var stack = catalog.ComposeStacks["gateway-headroom"];
        var template = LoadStackTemplate();

        var output = ComposeStackGenerator.GenerateStack(stack, catalog, template);

        Assert.Contains("/readyz", output);
    }
}
