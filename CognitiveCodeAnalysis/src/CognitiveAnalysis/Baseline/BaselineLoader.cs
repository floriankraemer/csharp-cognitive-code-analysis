/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static CognitiveBaselineSnapshot Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Baseline file path is required.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Baseline file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        using JsonDocument document = Parse(json, filePath);
        JsonElement root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"Baseline file '{filePath}' is empty or invalid JSON.");
        }

        // A newer layout is reported as such instead of as a list of unknown fields.
        if (root.ValueKind == JsonValueKind.Object
            && root.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, "schemaVersion", StringComparison.OrdinalIgnoreCase))
                is { Value.ValueKind: JsonValueKind.Number } versionProperty
            && versionProperty.Value.TryGetInt32(out int version)
            && version != CognitiveBaselineSnapshot.CurrentSchemaVersion)
        {
            throw UnsupportedVersion(version);
        }

        List<string> errors = BaselineValidator.Validate(root);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Invalid baseline file format in {filePath}: {string.Join(", ", errors)}");
        }

        var snapshot = root.Deserialize<CognitiveBaselineSnapshot>(JsonOptions)!;

        return snapshot;
    }

    private static InvalidOperationException UnsupportedVersion(int version) =>
        new($"Unsupported baseline schema version {version}. Expected version {CognitiveBaselineSnapshot.CurrentSchemaVersion}.");

    private static JsonDocument Parse(string json, string filePath)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Invalid baseline file format in {filePath}: {exception.Message}", exception);
        }
    }

    public static string Serialize(CognitiveBaselineSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);
}
