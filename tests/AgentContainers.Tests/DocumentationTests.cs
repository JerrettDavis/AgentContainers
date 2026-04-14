using System.Text.Json;

namespace AgentContainers.Tests;

/// <summary>
/// Documentation tests that keep the README, DocFX config, and matrix docs wired up.
/// </summary>
public class DocumentationTests
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

    [Fact]
    public void RootReadme_ContainsDocsAndMatrixSections()
    {
        var readmePath = Path.Combine(GetRepoRoot(), "README.md");
        var content = File.ReadAllText(readmePath);

        Assert.Contains("# AgentContainers", content);
        Assert.Contains("## Current image matrix", content);
        Assert.Contains("docs/docfx.json", content);
        Assert.Contains("node-bun", content);
        Assert.Contains("fullstack-polyglot", content);
        Assert.Contains("claude", content);
        Assert.Contains("headroom", content);
        Assert.Contains("solo-claude", content);
    }

    [Fact]
    public void DocfxConfig_ReferencesProjectsAndApiOutput()
    {
        var docfxPath = Path.Combine(GetRepoRoot(), "docs", "docfx.json");
        using var document = JsonDocument.Parse(File.ReadAllText(docfxPath));

        var metadata = document.RootElement.GetProperty("metadata");
        Assert.True(metadata.GetArrayLength() > 0);

        var firstMetadata = metadata[0];
        Assert.Equal("api", firstMetadata.GetProperty("dest").GetString());

        var srcFiles = firstMetadata.GetProperty("src")[0].GetProperty("files");
        Assert.Contains(srcFiles.EnumerateArray(), file => file.GetString() == "src/AgentContainers.Core/AgentContainers.Core.csproj");
        Assert.Contains(srcFiles.EnumerateArray(), file => file.GetString() == "src/AgentContainers.Generator/AgentContainers.Generator.csproj");

        var build = document.RootElement.GetProperty("build");
        var contentFiles = build.GetProperty("content")[0].GetProperty("files");
        Assert.Contains(contentFiles.EnumerateArray(), file => file.GetString() == "api/**.yml");
        Assert.Contains(contentFiles.EnumerateArray(), file => file.GetString() == "index.md");
    }

    [Fact]
    public void DocsToc_IncludesGuidesPlansAndApiReference()
    {
        var tocPath = Path.Combine(GetRepoRoot(), "docs", "toc.yml");
        var content = File.ReadAllText(tocPath);

        Assert.Contains("articles/toc.yml", content);
        Assert.Contains("plans/toc.yml", content);
        Assert.Contains("ENV-CONTRACT.md", content);
        Assert.Contains("e2e-testing.md", content);
        Assert.Contains("api/toc.yml", content);
    }

    [Fact]
    public void DockerfileMatrix_DocumentsAllImageFamilies()
    {
        var matrixPath = Path.Combine(GetRepoRoot(), "docs", "articles", "dockerfile-matrix.md");
        var content = File.ReadAllText(matrixPath);

        Assert.Contains("## Base Dockerfiles", content);
        Assert.Contains("## Combo Dockerfiles", content);
        Assert.Contains("## Agent overlay Dockerfiles", content);
        Assert.Contains("## Tool-pack overlay Dockerfiles", content);
        Assert.Contains("## Compose matrix", content);
        Assert.Contains("node-bun-openclaw", content);
        Assert.Contains("fullstack-polyglot-devtools", content);
    }
}
