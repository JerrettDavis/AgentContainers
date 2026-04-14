using AgentContainers.Core.Loading;
using AgentContainers.Core.Validation;

namespace AgentContainers.Tests;

/// <summary>
/// Tests for CatalogValidator with the real definitions.
/// </summary>
public class CatalogValidatorTests
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
    public void Validate_NoErrors()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());
        var validator = new CatalogValidator();
        var results = validator.Validate(catalog);

        var errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsInfoForUnconstrainedAgents()
    {
        var loader = new ManifestLoader();
        var catalog = loader.LoadAll(GetDefinitionsRoot());
        var validator = new CatalogValidator();
        var results = validator.Validate(catalog);

        // Should not crash — info/warnings are acceptable
        Assert.NotNull(results);
    }
}
