using AgentContainers.Core.Loading;
using AgentContainers.Core.Models;
using AgentContainers.Generator;

namespace AgentContainers.Tests;

/// <summary>
/// Tests for publishing-related features: platforms, OCI labels, build matrix output.
/// </summary>
public class PublishingTests
{
    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "definitions")))
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

    [Fact]
    public void Bases_HavePlatformsDefined()
    {
        var catalog = LoadCatalog();

        foreach (var (id, b) in catalog.Bases)
        {
            Assert.True(b.Platforms.Count > 0,
                $"Base '{id}' should have at least one platform defined.");
            Assert.Contains("linux/amd64", b.Platforms);
        }
    }

    [Fact]
    public void Bases_SupportMultiArch()
    {
        var catalog = LoadCatalog();

        // At least one base should support arm64
        Assert.True(catalog.Bases.Values.Any(b => b.Platforms.Contains("linux/arm64")),
            "At least one base should support linux/arm64.");
    }

    [Fact]
    public void GeneratedDockerfiles_ContainOciLabels()
    {
        var generatedRoot = Path.Combine(GetRepoRoot(), "generated", "docker");

        foreach (var dockerfile in Directory.GetFiles(generatedRoot, "Dockerfile", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(dockerfile);
            var relativePath = Path.GetRelativePath(generatedRoot, dockerfile);

            Assert.Contains("org.opencontainers.image.title", content,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("org.opencontainers.image.source", content,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("org.opencontainers.image.vendor", content,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("org.opencontainers.image.licenses", content,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GeneratedDockerfiles_ContainBuildTimeArgs()
    {
        var generatedRoot = Path.Combine(GetRepoRoot(), "generated", "docker");

        foreach (var dockerfile in Directory.GetFiles(generatedRoot, "Dockerfile", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(dockerfile);

            Assert.Contains("ARG BUILD_DATE", content);
            Assert.Contains("ARG VCS_REF", content);
            Assert.Contains("ARG IMAGE_VERSION", content);
        }
    }

    [Fact]
    public void GeneratedDockerfiles_HaveImageTypeLabel()
    {
        var generatedRoot = Path.Combine(GetRepoRoot(), "generated", "docker");

        // Base images should have image-type=base
        foreach (var baseDir in Directory.GetDirectories(Path.Combine(generatedRoot, "bases")))
        {
            var dockerfile = Path.Combine(baseDir, "Dockerfile");
            if (!File.Exists(dockerfile)) continue;
            var content = File.ReadAllText(dockerfile);
            Assert.Contains("dev.agentcontainers.image-type=\"base\"", content);
        }

        // Combo images should have image-type=combo
        var combosDir = Path.Combine(generatedRoot, "combos");
        if (Directory.Exists(combosDir))
        {
            foreach (var comboDir in Directory.GetDirectories(combosDir))
            {
                var dockerfile = Path.Combine(comboDir, "Dockerfile");
                if (!File.Exists(dockerfile)) continue;
                var content = File.ReadAllText(dockerfile);
                Assert.Contains("dev.agentcontainers.image-type=\"combo\"", content);
            }
        }
    }

    [Fact]
    public void ImageCatalog_ContainsRegistryInfo()
    {
        var catalogPath = Path.Combine(GetRepoRoot(), "generated", "image-catalog.json");
        var content = File.ReadAllText(catalogPath);

        Assert.Contains("\"registry\"", content);
        Assert.Contains("ghcr.io/agentcontainers", content);
    }

    [Fact]
    public void ImageCatalog_ContainsPlatforms()
    {
        var catalogPath = Path.Combine(GetRepoRoot(), "generated", "image-catalog.json");
        var content = File.ReadAllText(catalogPath);

        Assert.Contains("\"platforms\"", content);
        Assert.Contains("linux/amd64", content);
        Assert.Contains("linux/arm64", content);
    }
}
