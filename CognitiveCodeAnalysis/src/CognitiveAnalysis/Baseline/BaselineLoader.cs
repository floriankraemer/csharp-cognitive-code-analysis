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
        var snapshot = JsonSerializer.Deserialize<CognitiveBaselineSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Baseline file '{filePath}' is empty or invalid JSON.");

        if (snapshot.SchemaVersion != CognitiveBaselineSnapshot.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported baseline schema version {snapshot.SchemaVersion}. "
                + $"Expected version {CognitiveBaselineSnapshot.CurrentSchemaVersion}.");
        }

        return snapshot;
    }

    public static string Serialize(CognitiveBaselineSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);
}
