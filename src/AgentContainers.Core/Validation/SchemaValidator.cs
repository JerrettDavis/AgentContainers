using NJsonSchema;
using NJsonSchema.Validation;

namespace AgentContainers.Core.Validation;

/// <summary>
/// Validates raw YAML/JSON content against JSON Schema files.
/// Provides schema-level validation before deserialization.
/// </summary>
public sealed class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _schemas = [];

    /// <summary>
    /// Loads all .schema.json files from the schemas directory.
    /// </summary>
    public async Task LoadSchemasAsync(string schemasDirectory)
    {
        if (!Directory.Exists(schemasDirectory))
            return;

        foreach (var file in Directory.GetFiles(schemasDirectory, "*.schema.json"))
        {
            var schemaName = Path.GetFileNameWithoutExtension(file)
                .Replace(".schema", string.Empty);
            var json = await File.ReadAllTextAsync(file);
            var schema = await JsonSchema.FromJsonAsync(json);
            _schemas[schemaName] = schema;
        }
    }

    /// <summary>
    /// Returns the names of all loaded schemas.
    /// </summary>
    public IReadOnlyCollection<string> LoadedSchemas => _schemas.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Validates JSON content against a named schema.
    /// Returns empty list if schema not found (permissive in bootstrap).
    /// </summary>
    public ICollection<NJsonSchema.Validation.ValidationError> Validate(string schemaName, string jsonContent)
    {
        if (!_schemas.TryGetValue(schemaName, out var schema))
            return [];

        return schema.Validate(jsonContent);
    }
}
