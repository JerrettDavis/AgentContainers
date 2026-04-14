using AgentContainers.Core.Hashing;

namespace AgentContainers.Tests;

/// <summary>
/// Tests for ContentHasher — deterministic hashing of definitions and content.
/// </summary>
public class ContentHasherTests
{
    private static string GetDefinitionsRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "definitions");
            if (Directory.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not find definitions/ directory.");
    }

    [Fact]
    public void ComputeManifestHash_IsDeterministic()
    {
        var root = GetDefinitionsRoot();
        var hash1 = ContentHasher.ComputeManifestHash(root);
        var hash2 = ContentHasher.ComputeManifestHash(root);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeManifestHash_ReturnsValidHex()
    {
        var root = GetDefinitionsRoot();
        var hash = ContentHasher.ComputeManifestHash(root);

        // SHA-256 produces 64 hex characters
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeContentHash_SameInputSameHash()
    {
        var content = "Hello, world!";
        var hash1 = ContentHasher.ComputeContentHash(content);
        var hash2 = ContentHasher.ComputeContentHash(content);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_DifferentInputDifferentHash()
    {
        var hash1 = ContentHasher.ComputeContentHash("Hello");
        var hash2 = ContentHasher.ComputeContentHash("World");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_NormalizesLineEndings()
    {
        var hashCrlf = ContentHasher.ComputeContentHash("line1\r\nline2\r\n");
        var hashLf = ContentHasher.ComputeContentHash("line1\nline2\n");

        Assert.Equal(hashCrlf, hashLf);
    }

    [Fact]
    public void NormalizePath_ConvertsBackslashesToForwardSlashes()
    {
        Assert.Equal("docker/bases/dotnet/Dockerfile",
            ContentHasher.NormalizePath(@"docker\bases\dotnet\Dockerfile"));
    }

    [Fact]
    public void NormalizePath_PreservesForwardSlashes()
    {
        Assert.Equal("compose/stacks/solo-claude/docker-compose.yaml",
            ContentHasher.NormalizePath("compose/stacks/solo-claude/docker-compose.yaml"));
    }
}
