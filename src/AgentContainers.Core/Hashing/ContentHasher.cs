using System.Security.Cryptography;
using System.Text;

namespace AgentContainers.Core.Hashing;

/// <summary>
/// Deterministic content hashing for manifest definitions and generated artifacts.
/// All hashing normalizes line endings (CRLF → LF) and path separators (\ → /)
/// so the same logical content produces the same hash on any platform.
/// </summary>
public static class ContentHasher
{
    /// <summary>
    /// Computes a single SHA-256 hash over all YAML definitions in a directory tree.
    /// Files are processed in stable sorted order by their normalized relative path.
    /// </summary>
    public static string ComputeManifestHash(string definitionsRoot)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var files = Directory.GetFiles(definitionsRoot, "*.yaml", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(definitionsRoot, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        foreach (var relPath in files)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(relPath));
            sha.AppendData(Encoding.UTF8.GetBytes("\n"));

            var fullPath = Path.Combine(definitionsRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(fullPath).ReplaceLineEndings("\n");
            sha.AppendData(Encoding.UTF8.GetBytes(content));
            sha.AppendData(Encoding.UTF8.GetBytes("\n"));
        }

        return Convert.ToHexString(sha.GetCurrentHash()).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA-256 hash of a string, normalizing line endings first.
    /// </summary>
    public static string ComputeContentHash(string content)
    {
        var normalized = content.ReplaceLineEndings("\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes a relative file path to use forward slashes.
    /// </summary>
    public static string NormalizePath(string path) => path.Replace('\\', '/');
}
